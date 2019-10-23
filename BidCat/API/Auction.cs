using ApiListener;
using BidCat.Banks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using BidCat.DataStructs;

namespace BidCat.API
{
	public class Auction
	{
		// Lot ID, User ID, Amount
		private Dictionary<string, List<BidTuple>> lotDict = new Dictionary<string, List<BidTuple>>();

		private List<string> ChangesTracker = new List<string>();
		private bool SoftCooldownEnabled;
		private bool HardCooldownEnabled;

		private Dictionary<string, Cooldown> cooldownDict = new Dictionary<string, Cooldown>();

		private Action<ApiLogMessage> Logger;
		private Cooldown.HardCooldown DefaultHardCooldown = new Cooldown.HardCooldown();
		private Cooldown.SoftCooldown DefaultSoftCooldown = new Cooldown.SoftCooldown();

		public AbstractBank bank;
		public Auction(Action<ApiLogMessage> logger, AbstractBank bank)
		{
			Logger = logger;
			this.bank = bank;
			this.bank.RegisterReservedMoneyChecker(GetReservedMoney);
		}

		~Auction()
		{
			bank.Dispose();
			foreach (KeyValuePair<string, Cooldown> cooldowns in cooldownDict)
			{
				foreach (Cooldown.SoftCooldown softCooldown in cooldowns.Value.softCooldowns)
				{
					softCooldown.CooldownTimer.Stop();
					softCooldown.CooldownTimer.Dispose();
				}
			}
		}

		private async Task<int> GetReservedMoney(int userId)
		{
			return (await GetBidsForUser(userId)).Sum(x => x.Value);
		}

		public async Task<Dictionary<int, int>> GetReservedMoney(IEnumerable<int> userIds)
		{
			Dictionary<int, int> ret = new Dictionary<int, int>();
			foreach (int userId in userIds)
				ret.Add(userId, await GetReservedMoney(userId));
			return ret;
		}

		public Task<Dictionary<string, int>> GetBidsForUser(int userId) => Task.Run(() =>
		{
			Dictionary<string, int> bids = new Dictionary<string, int>();
			foreach (KeyValuePair<string, List<BidTuple>> pair in lotDict)
			{
				if (pair.Value.Any(x => x.UserId == userId))
					bids.Add(pair.Key, pair.Value.First(x => x.UserId == userId).Amount);
			}

			return bids;
		});

		public async Task<Dictionary<int, Dictionary<string, int>>> GetBidsForUsers(IEnumerable<int> userIds)
		{
			Dictionary<int, Dictionary<string, int>> ret = new Dictionary<int, Dictionary<string, int>>();
			foreach (int userId in userIds)
				ret.Add(userId, await GetBidsForUser(userId));
			return ret;
		}

		public Task<Cooldown> GetCooldown(string lotId) => Task.Run(() =>
		{
			cooldownDict.TryGetValue(lotId, out Cooldown value);
			return value;
		});

		public async Task<Dictionary<string, Cooldown>> GetCooldowns(IEnumerable<string> lotIds)
		{
			Dictionary<string, Cooldown> ret = new Dictionary<string, Cooldown>();
			foreach (string id in lotIds)
				ret.Add(id, await GetCooldown(id));
			return ret;
		}

		public async Task Clear()
		{
			Dictionary<string, object> winner = await GetWinner();
			string winningLot = (string) winner["item"];
			Cooldown cooldown = cooldownDict.ContainsKey(winningLot) ? cooldownDict[winningLot] : new Cooldown
			{
				hardCooldown = DefaultHardCooldown
			};

			if (SoftCooldownEnabled)
			{
				Cooldown.SoftCooldown softCooldown = new Cooldown.SoftCooldown(DefaultSoftCooldown)
				{
					CooldownActive = true
				};
				if (!DefaultSoftCooldown.CooldownLotBased)
				{
					softCooldown.DueTime = DateTime.UtcNow.AddMilliseconds(softCooldown.CooldownLength);
					softCooldown.CooldownTimer = new Timer((softCooldown.DueTime - DateTime.UtcNow).TotalMilliseconds);
					softCooldown.CooldownTimer.Elapsed += delegate { SoftTimerElapsed(winningLot, softCooldown); };
					softCooldown.CooldownTimer.Start();
					Logger(new ApiLogMessage($"Soft cooldown for lot {winningLot} added, it will expire at {softCooldown.DueTime:o}", ApiLogLevel.Info));
				}
				cooldown.softCooldowns.Add(softCooldown);
			}

			if (HardCooldownEnabled)
			{
				cooldown.hardCooldown = new Cooldown.HardCooldown(DefaultHardCooldown) {CooldownActive = true};
				if (!cooldown.hardCooldown.CooldownLotBased)
				{
					cooldown.hardCooldown.DueTime =
						DateTime.UtcNow.AddMilliseconds(cooldown.hardCooldown.CooldownLength);
					Console.WriteLine(cooldown.hardCooldown.DueTime.ToString("o"));
					cooldown.hardCooldown.CooldownTimer = new Timer((cooldown.hardCooldown.DueTime - DateTime.UtcNow).TotalMilliseconds);
					cooldown.hardCooldown.CooldownTimer.Elapsed += delegate { HardTimerElapsed(winningLot); };
					cooldown.hardCooldown.CooldownTimer.Start();
					Logger(new ApiLogMessage($"Hard cooldown for lot {winningLot} will expire at {cooldown.hardCooldown.DueTime:o}", ApiLogLevel.Info));
				}
			}
			if (!cooldownDict.ContainsKey(winningLot))
				cooldownDict.Add(winningLot, cooldown);

			List<string> toRemove = new List<string>();

			foreach (KeyValuePair<string, Cooldown> kvp in cooldownDict.Where(x =>
				x.Value.softCooldowns.Any(y => y.CooldownLotBased) && x.Key != winningLot && x.Value.softCooldowns.Any(y => y.CooldownActive)))
			{
				foreach (Cooldown.SoftCooldown softCooldown in kvp.Value.softCooldowns.Where(x =>
					x.CooldownLotBased && x.CooldownActive))
				{
					softCooldown.CooldownLength--;
					if (softCooldown.CooldownLength != 0) continue;
					softCooldown.CooldownActive = false;
					kvp.Value.softCooldowns.Remove(softCooldown);
					if (!kvp.Value.hardCooldown.CooldownActive && kvp.Value.softCooldowns.Count == 0) toRemove.Add(kvp.Key);
				}
			}

			foreach (KeyValuePair<string, Cooldown> kvp in cooldownDict.Where(
				x => x.Value.hardCooldown.CooldownLotBased && x.Key != winningLot && x.Value.hardCooldown.CooldownActive))
			{
				kvp.Value.hardCooldown.CooldownLength--;
				if (kvp.Value.hardCooldown.CooldownLength != 0) continue;
				kvp.Value.hardCooldown.CooldownActive = false;
				if (kvp.Value.softCooldowns.Count == 0) toRemove.Add(kvp.Key);
			}

			foreach (string key in toRemove)
			{
				cooldownDict.Remove(key);
			}
			lotDict.Clear();
		}

		public async Task PlaceBid(int user, string item, int amount)
		{
			await HandleBid(user, item, amount);
		}

		public async Task PlaceBids(IEnumerable<Tuple<int, string, int>> bids)
		{
			foreach ((int user, string item, int amount) in bids)
				await PlaceBid(user, item, amount);
		}

		public async Task ReplaceBid(int user, string item, int amount, bool allowVisibleLowering = false, bool allowNewBids = false)
		{
			if (!allowNewBids)
			{
				await HandleBid(user, item, amount, true, allowVisibleLowering);
			}
			else
			{
				try
				{
					await HandleBid(user, item, amount, true, allowVisibleLowering);
				}
				catch (NoExistingBidError)
				{
					await HandleBid(user, item, amount, false, allowVisibleLowering);
				}
			}
		}

		public async Task ReplaceBids(IEnumerable<Tuple<int, string, int>> bids, bool allowVisibleLowering = false, bool allowNewBids = false)
		{
			foreach ((int user, string item, int amount) in bids)
				await ReplaceBid(user, item, amount, allowVisibleLowering, allowNewBids);
		}

		public async Task IncreaseBid(int user, string item, int amount)
		{
			if (!lotDict.ContainsKey(item))
				throw new NoExistingBidError("There is no bid from that user on that item which could be replaced.");
			BidTuple userObj = lotDict[item].FirstOrDefault(x => x.UserId == user);
			if (userObj == null)
				throw new NoExistingBidError("There is no bid from that user on that item which could be replaced.");
			int previousBid = userObj.Amount;
			await ReplaceBid(user, item, amount + previousBid);
		}

		public async Task IncreaseBids(IEnumerable<Tuple<int, string, int>> bids)
		{
			foreach ((int user, string item, int amount) in bids)
				await IncreaseBid(user, item, amount);
		}

		public Task<bool> RemoveBid(int user, string item) => Task.Run(() =>
		{
			if (!lotDict.ContainsKey(item) || lotDict[item].FirstOrDefault(x => x.UserId == user) == null)
			{
				return false;
			}

			lotDict[item].Remove(lotDict[item].First(x => x.UserId == user));
			if (lotDict[item].Any())
				UpdateLastChange(item);
			else
			{
				lotDict.Remove(item);
				ChangesTracker.Remove(item);
			}

			return true;
		});

		public async Task<Dictionary<int, bool>> RemoveBids(IEnumerable<Tuple<int, string>> bids)
		{
			Dictionary<int, bool> ret = new Dictionary<int, bool>();
			foreach ((int user, string item) in bids)
				ret.Add(user, await RemoveBid(user, item));
			return ret;
		}

		public Task<bool> RemoveAllBids(int user) => Task.Run(() =>
		{
			List<string> list =
				lotDict.Where(x => x.Value.Any(y => y.UserId == user)).Select(x => x.Key).ToList();
			if (!list.Any())
				return false;

			foreach (string item in list)
			{
				lotDict[item].Remove(lotDict[item].First(x => x.UserId == user));
				if (lotDict[item].Any())
					UpdateLastChange(item);
				else
				{
					lotDict.Remove(item);
					ChangesTracker.Remove(item);
				}
			}
			return true;
		});

		public Task<List<BidTuple>> GetBidsForItem(string item) => Task.Run(() =>
		{
			if (!lotDict.ContainsKey(item))
				throw new ApiError("The specified lot does not exist");
			return lotDict[item];
		});

		public async Task<Dictionary<string, List<BidTuple>>> GetBidsForItems(IEnumerable<string> items)
		{
			Dictionary<string, List<BidTuple>> ret = new Dictionary<string, List<BidTuple>>();
			foreach (string item in items)
				ret.Add(item, await GetBidsForItem(item));
			return ret;
		}

		public Task<Dictionary<string, List<BidTuple>>> GetAllBids() => Task.Run(() => lotDict);

		public Task RegisterSoftCooldown(int cooldownLength, int cooldownIncrementLength, int cooldownMinBid,
			int cooldownIncrementAmount, bool lotBased) => Task.Run(() =>
		{
			SoftCooldownEnabled = true;
			DefaultSoftCooldown.CooldownLength = cooldownLength;
			DefaultSoftCooldown.CooldownIncrementAmount = cooldownIncrementAmount;
			DefaultSoftCooldown.CooldownIncrementLength = cooldownIncrementLength;
			DefaultSoftCooldown.CooldownMinimumBid = cooldownMinBid;
			DefaultSoftCooldown.CooldownLotBased = lotBased;
		});

		public Task DeregisterSoftCooldown() => Task.Run(() =>
		{
			SoftCooldownEnabled = false;
			DefaultSoftCooldown = new Cooldown.SoftCooldown();
		});

		public Task RegisterHardCooldown(int cooldownLength, bool lotBased) => Task.Run(() =>
		{
			HardCooldownEnabled = true;
			DefaultHardCooldown.CooldownLength = cooldownLength;
			DefaultHardCooldown.CooldownLotBased = lotBased;
		});

		public Task DeregisterHardCooldown() => Task.Run(() =>
		{
			HardCooldownEnabled = false;
			DefaultHardCooldown = new Cooldown.HardCooldown();
		});

		private void UpdateLastChange(string item)
		{
			if (ChangesTracker.Contains(item))
				ChangesTracker.Remove(item);
			ChangesTracker.Add(item);
		}

		private void SoftTimerElapsed(string item, Cooldown.SoftCooldown softCooldown)
		{
			Cooldown cooldown = cooldownDict[item];
			softCooldown.CooldownTimer.Stop();
			softCooldown.CooldownTimer.Dispose();
			softCooldown.CooldownTimer = null;
			softCooldown.CooldownActive = false;
			cooldown.softCooldowns.Remove(softCooldown);
			if (!cooldown.hardCooldown.CooldownActive && cooldown.softCooldowns.Count == 0)
				cooldownDict.Remove(item);
		}

		private void HardTimerElapsed(string item)
		{
			Cooldown cooldown = cooldownDict[item];
			cooldown.hardCooldown.CooldownTimer.Stop();
			cooldown.hardCooldown.CooldownTimer.Dispose();
			cooldown.hardCooldown.CooldownTimer = null;
			cooldown.hardCooldown.CooldownActive = false;
			if (cooldown.softCooldowns.Count == 0)
				cooldownDict.Remove(item);
		}

		private async Task HandleBid(int user, string item, int amount, bool replace = false,
			bool allowVisibleLowering = false)
		{
			if (amount < 1)
				throw new ApiError("Amount must be a number above 0");
			if (cooldownDict.ContainsKey(item))
			{
				Cooldown cooldown = cooldownDict[item];
				int minbid = cooldown.softCooldowns.Where(x => x.CooldownActive).Sum(x => x.CooldownMinimumBid);
				if (cooldown.hardCooldown.CooldownActive)
					throw new BiddingError("You currently cannot bid on this item.");
				if (amount < minbid)
					throw new BidTooLowError(
						$"The minimum bid for this item is currently {minbid}, you cannot bid {amount}");
			}

			int? previousBid = null;
			if (lotDict.TryGetValue(item, out _))
				previousBid = lotDict[item].FirstOrDefault(x => x.UserId == user)?.Amount;

			bool alreadyBid = previousBid != null;

			if (!replace && alreadyBid)
				throw new AlreadyBidError("There is already a bid from that user on that item.");
			if (replace && !alreadyBid)
				throw new NoExistingBidError("There is no bid from that user on that item which could be replaced.");
			if (replace && previousBid == amount)
				return;
			int neededMoney = amount;
			if (replace)
				neededMoney -= (int)previousBid;
			int availableMoney = await bank.GetAvailableMoney(user);
			if (neededMoney > availableMoney)
				throw new InsufficientMoneyError($"Can't afford to bid {neededMoney}, only {availableMoney} available.");
			if (replace && amount < previousBid && !allowVisibleLowering)
			{
				Dictionary<string, object> winner = await GetWinner();
				if ((string)winner["item"] != item)
					throw new VisiblyLoweredError();
				int headroom = (int) winner["total_bid"] - (int) winner["total_charge"];
				int decrease = (int)previousBid - amount;
				if (decrease > headroom)
					throw new VisiblyLoweredError();
			}

			UpdateLastChange(item);
			if (!lotDict.ContainsKey(item))
				lotDict[item] = new List<BidTuple>();
			if (replace && lotDict[item].Any(x => x.UserId == user))
				lotDict[item].Remove(lotDict[item].First(x => x.UserId == user));
			lotDict[item].Add(new BidTuple{ Amount = amount, UserId = user });
		}

		public Task<List<LotBidsTuple>> GetAllBidsOrdered() => Task.Run(() =>
		{
			List<LotBidsTuple> result = new List<LotBidsTuple>();
			foreach (KeyValuePair<string, List<BidTuple>> kvp in lotDict)
			{
				LotBidsTuple tuple =
					new LotBidsTuple { LotId = kvp.Key, Bids = kvp.Value };
				result.Add(tuple);
			}

			result = result.OrderByDescending(x => x.Bids.Sum(y => y.Amount)).ToList();

			return result;
		});

		public async Task<Dictionary<string, object>> GetWinner(bool discountLatter = false)
		{
			List<LotBidsTuple> bids = await GetAllBidsOrdered();
			if (!bids.Any())
				return null;
			(string winningItem, List<BidTuple> winningBids) = bids[0];
			int secondBid = 0;
			if (bids.Count > 1)
			{
				List<BidTuple> secondItemBids = bids[1].Bids;
				secondBid = secondItemBids.Sum(x => x.Amount);
			}

			int totalBid = winningBids.Sum(x => x.Amount);
			int overpaid = Math.Max(0, totalBid - secondBid - 1);
			int totalCharge = totalBid - overpaid;
			List<MutableTuple<int, int>> moneyOwed = (from userAmountTuple in winningBids.OrderByDescending(x => x.Amount) let percentage = userAmountTuple.Amount / (double) totalBid select new MutableTuple<int, int>(userAmountTuple.UserId, (int) Math.Ceiling(totalCharge * percentage))).ToList();
			overpaid = moneyOwed.Sum(x => x.Item2) - totalCharge;
			List<MutableTuple<int, int>> array = new List<MutableTuple<int, int>>();
			array.AddRange(moneyOwed);
			if (discountLatter) array.Reverse();
			while (overpaid > 0)
			{
				if (overpaid / array.Count >= 1)
				{
					double perUserDiscount = (double) overpaid / array.Count;
					int floorDiscount = (int)Math.Floor(perUserDiscount);
					foreach (MutableTuple<int, int> user in array)
					{
						MutableTuple<int, int> temp = moneyOwed.First(x => x.Item1 == user.Item1);
						temp.Item2 -= floorDiscount;
					}

					overpaid -= floorDiscount * array.Count;
					if (overpaid == 0) break;
				}
				foreach (MutableTuple<int, int> user in array)
				{
					MutableTuple<int, int> temp = moneyOwed.First(x => x.Item1 == user.Item1);
					temp.Item2--;
					overpaid--;
					if (overpaid == 0) break;
				}
			}

			return new Dictionary<string, object>
			{
				{ "item", winningItem },
				{ "total_bid", totalBid },
				{ "total_charge", totalCharge },
				{ "money_owed", moneyOwed.Select(x => new MoneyOwed(x)).ToList() }
			};
		}
	}
}
