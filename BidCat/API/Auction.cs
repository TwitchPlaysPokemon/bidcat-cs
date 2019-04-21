using ApiListener;
using BidCat.Banks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace BidCat.API
{
	public class Auction
	{
		// Lot ID, User ID, Amount
		private Dictionary<string, List<Tuple<int, int>>> lotDict = new Dictionary<string, List<Tuple<int, int>>>();

		private List<string> ChangesTracker = new List<string>();
		private bool SoftCooldownEnabled;
		private bool HardCooldownEnabled;

		private Dictionary<string, Cooldown> cooldownDict;

		private Cooldown DefaultCooldown = new Cooldown();

		public AbstractBank bank;
		public Auction(Action<ApiLogMessage> logger, AbstractBank bank)
		{
			this.bank = bank;
			this.bank.RegisterReservedMoneyChecker(GetReservedMoney);
		}

		~Auction()
		{
			bank.DeregisterReservedMoneyChecker(GetReservedMoney);
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

#pragma warning disable 1998
		public async Task<Dictionary<string, int>> GetBidsForUser(int userId)
		{
			Dictionary<string, int> bids = new Dictionary<string, int>();
			foreach (KeyValuePair<string, List<Tuple<int, int>>> pair in lotDict)
			{
				if (pair.Value.Any(x => x.Item1 == userId))
					bids.Add(pair.Key, pair.Value.First(x => x.Item1 == userId).Item2);
			}
			return bids;
		}
#pragma warning restore 1998

		public async Task<Dictionary<int, Dictionary<string, int>>> GetBidsForUsers(IEnumerable<int> userIds)
		{
			Dictionary<int, Dictionary<string, int>> ret = new Dictionary<int, Dictionary<string, int>>();
			foreach (int userId in userIds)
				ret.Add(userId, await GetBidsForUser(userId));
			return ret;
		}

#pragma warning disable 1998
		public async Task<Cooldown> GetCooldown(string lotId)
		{
			cooldownDict.TryGetValue(lotId, out Cooldown value);
			return value;
		}
#pragma warning restore 1998

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
			Cooldown cooldown = cooldownDict.ContainsKey(winningLot) ? cooldownDict[winningLot] : new Cooldown(DefaultCooldown);

			if (SoftCooldownEnabled)
			{
				if (cooldown.softCooldown.CooldownActive)
				{
					cooldown.softCooldown.CooldownLength += cooldown.softCooldown.CooldownIncrementLength;
					cooldown.softCooldown.CooldownMinimumBid += cooldown.softCooldown.CooldownIncrementAmount;
					if (!cooldown.softCooldown.CooldownLotBased)
					{
						cooldown.softCooldown.DueTime =
							cooldown.softCooldown.DueTime.AddMilliseconds(cooldown.softCooldown
								.CooldownIncrementLength);
						cooldown.softCooldown.CooldownTimer.Stop();
						cooldown.softCooldown.CooldownTimer.Dispose();
						cooldown.softCooldown.CooldownTimer = new Timer(cooldown.softCooldown.CooldownLength);
						cooldown.softCooldown.CooldownTimer.Elapsed += delegate { SoftTimerElapsed(winningLot); };
						cooldown.softCooldown.CooldownTimer.Start();
					}
				}
				else
				{
					cooldown.softCooldown = new Cooldown.SoftCooldown(DefaultCooldown.softCooldown)
					{
						CooldownActive = true
					};
					if (!cooldown.softCooldown.CooldownLotBased)
					{
						cooldown.softCooldown.DueTime =
							DateTime.UtcNow.AddMilliseconds(cooldown.softCooldown.CooldownLength);
						cooldown.softCooldown.CooldownTimer = new Timer(cooldown.softCooldown.CooldownLength);
						cooldown.softCooldown.CooldownTimer.Elapsed += delegate { SoftTimerElapsed(winningLot); };
						cooldown.softCooldown.CooldownTimer.Start();
					}
				}
			}

			if (HardCooldownEnabled)
			{
				cooldown.hardCooldown = new Cooldown.HardCooldown(DefaultCooldown.hardCooldown) {CooldownActive = true};
				if (!cooldown.hardCooldown.CooldownLotBased)
				{
					cooldown.hardCooldown.DueTime =
						DateTime.UtcNow.AddMilliseconds(cooldown.hardCooldown.CooldownLength);
					cooldown.hardCooldown.CooldownTimer = new Timer(cooldown.hardCooldown.CooldownLength);
					cooldown.hardCooldown.CooldownTimer.Elapsed += delegate { HardTimerElapsed(winningLot); };
					cooldown.hardCooldown.CooldownTimer.Start();
				}
			}
			if (!cooldownDict.ContainsKey(winningLot))
				cooldownDict.Add(winningLot, cooldown);

			foreach (KeyValuePair<string, Cooldown> kvp in cooldownDict.Where(x =>
				x.Value.softCooldown.CooldownLotBased && x.Key != winningLot))
			{
				kvp.Value.softCooldown.CooldownLength--;
				if (kvp.Value.softCooldown.CooldownLength != 0) continue;
				kvp.Value.softCooldown.CooldownActive = false;
				if (!kvp.Value.hardCooldown.CooldownActive) cooldownDict.Remove(kvp.Key);
			}

			foreach (KeyValuePair<string, Cooldown> kvp in cooldownDict.Where(
				x => x.Value.hardCooldown.CooldownLotBased && x.Key != winningLot))
			{
				kvp.Value.hardCooldown.CooldownLength--;
				if (kvp.Value.hardCooldown.CooldownLength != 0) continue;
				kvp.Value.hardCooldown.CooldownActive = false;
				if (!kvp.Value.softCooldown.CooldownActive) cooldownDict.Remove(kvp.Key);
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

		public async Task ReplaceBid(int user, string item, int amount, bool allowVisibleLowering = false)
		{
			await HandleBid(user, item, amount, true, allowVisibleLowering);
		}

		public async Task ReplaceBids(IEnumerable<Tuple<int, string, int>> bids, bool allowVisibleLowering = false)
		{
			foreach ((int user, string item, int amount) in bids)
				await ReplaceBid(user, item, amount, allowVisibleLowering);
		}

		public async Task IncreaseBid(int user, string item, int amount)
		{
			if (!lotDict.ContainsKey(item))
				throw new NoExistingBidError("There is no bid from that user on that item which could be replaced.");
			Tuple<int, int> userObj = lotDict[item].FirstOrDefault(x => x.Item1 == user);
			if (userObj == null)
				throw new NoExistingBidError("There is no bid from that user on that item which could be replaced.");
			int previousBid = userObj.Item2;
			await ReplaceBid(user, item, amount + previousBid);
		}

		public async Task IncreaseBids(IEnumerable<Tuple<int, string, int>> bids)
		{
			foreach ((int user, string item, int amount) in bids)
				await IncreaseBid(user, item, amount);
		}

#pragma warning disable 1998
		public async Task<bool> RemoveBid(int user, string item)
		{
			if (!lotDict.ContainsKey(item) || lotDict[item].FirstOrDefault(x => x.Item1 == user) == null)
			{
				return false;
			}

			lotDict[item].Remove(lotDict[item].First(x => x.Item1 == user));
			if (lotDict[item].Any())
				UpdateLastChange(item);
			else
			{
				lotDict.Remove(item);
				ChangesTracker.Remove(item);
			}

			return true;
		}
#pragma warning restore 1998

		public async Task<Dictionary<int, bool>> RemoveBids(IEnumerable<Tuple<int, string>> bids)
		{
			Dictionary<int, bool> ret = new Dictionary<int, bool>();
			foreach ((int user, string item) in bids)
				ret.Add(user, await RemoveBid(user, item));
			return ret;
		}

#pragma warning disable 1998
		public async Task<List<Tuple<int, int>>> GetBidsForItem(string item)
		{
			if (!lotDict.ContainsKey(item))
				throw new ApiError("The specified lot does not exist");
			return lotDict[item];
		}
#pragma warning restore 1998

		public async Task<Dictionary<string, List<Tuple<int, int>>>> GetBidsForItems(IEnumerable<string> items)
		{
			Dictionary<string, List<Tuple<int, int>>> ret = new Dictionary<string, List<Tuple<int, int>>>();
			foreach (string item in items)
				ret.Add(item, await GetBidsForItem(item));
			return ret;
		}

#pragma warning disable 1998
		public async Task<Dictionary<string, List<Tuple<int, int>>>> GetAllBids() => lotDict;

		public async Task RegisterSoftCooldown(int cooldownLength, int cooldownIncrementLength, int cooldownMinBid,
			int cooldownIncrementAmount, bool lotBased)
		{
			SoftCooldownEnabled = true;
			DefaultCooldown.softCooldown.CooldownLength = cooldownLength;
			DefaultCooldown.softCooldown.CooldownIncrementAmount = cooldownIncrementAmount;
			DefaultCooldown.softCooldown.CooldownIncrementLength = cooldownIncrementLength;
			DefaultCooldown.softCooldown.CooldownMinimumBid = cooldownMinBid;
			DefaultCooldown.softCooldown.CooldownLotBased = lotBased;
		}

		public async Task DeregisterSoftCooldown()
		{
			SoftCooldownEnabled = false;
			DefaultCooldown.softCooldown = new Cooldown.SoftCooldown();
		}

		public async Task RegisterHardCooldown(int cooldownLength, bool lotBased)
		{
			HardCooldownEnabled = true;
			DefaultCooldown.hardCooldown.CooldownLength = cooldownLength;
			DefaultCooldown.hardCooldown.CooldownLotBased = lotBased;
		}

		public async Task DeregisterHardCooldown()
		{
			HardCooldownEnabled = false;
			DefaultCooldown.hardCooldown = new Cooldown.HardCooldown();
		}
#pragma warning restore 1998

		private void UpdateLastChange(string item)
		{
			if (ChangesTracker.Contains(item))
				ChangesTracker.Remove(item);
			ChangesTracker.Add(item);
		}

		private void SoftTimerElapsed(string item)
		{
			Cooldown cooldown = cooldownDict[item];
			cooldown.softCooldown.CooldownTimer.Stop();
			cooldown.softCooldown.CooldownTimer.Dispose();
			cooldown.softCooldown.CooldownTimer = null;
			cooldown.softCooldown.CooldownActive = false;
			if (!cooldown.hardCooldown.CooldownActive)
				cooldownDict.Remove(item);
		}

		private void HardTimerElapsed(string item)
		{
			Cooldown cooldown = cooldownDict[item];
			cooldown.hardCooldown.CooldownTimer.Stop();
			cooldown.hardCooldown.CooldownTimer.Dispose();
			cooldown.hardCooldown.CooldownTimer = null;
			cooldown.hardCooldown.CooldownActive = false;
			if (!cooldown.softCooldown.CooldownActive)
				cooldownDict.Remove(item);
		}

		private async Task HandleBid(int user, string item, int amount, bool replace = false,
			bool allowVisibleLowering = false)
		{
			if (amount < 1)
			{
				throw new ApiError("Amount must be a number above 0");
			}

			int? previousBid = null;
			if (lotDict.TryGetValue(item, out _))
				previousBid = lotDict[item].FirstOrDefault(x => x.Item1 == user)?.Item2;

			bool alreadyBid = previousBid != null;
			if (cooldownDict.ContainsKey(item))
			{
				Cooldown cooldown = cooldownDict[item];
				if (amount < cooldown.softCooldown.CooldownMinimumBid && cooldown.softCooldown.CooldownActive)
					throw new BidTooLowError(
						$"The minimum bid for this item is currently {cooldown.softCooldown.CooldownMinimumBid}, you cannot bid {amount}");
				if (cooldown.hardCooldown.CooldownActive)
					throw new BiddingError("You currently cannot bid on this item.");
			}

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
				lotDict[item] = new List<Tuple<int, int>>();
			lotDict[item].Add(new Tuple<int, int>(user, amount));
		}

#pragma warning disable 1998
		public async Task<List<Tuple<string, List<Tuple<int, int>>>>> GetAllBidsOrdered()
		{
			List<Tuple<string, List<Tuple<int, int>>>> result = new List<Tuple<string, List<Tuple<int, int>>>>();
			foreach (KeyValuePair<string, List<Tuple<int, int>>> kvp in lotDict)
			{
				Tuple<string, List<Tuple<int, int>>> tuple =
					new Tuple<string, List<Tuple<int, int>>>(kvp.Key, kvp.Value);
				result.Add(tuple);
			}

			return result.OrderByDescending(x => x.Item2.Sum(y => y.Item2)).ToList();
		}
#pragma warning restore 1998

		public async Task<Dictionary<string, object>> GetWinner(bool discountLatter = false)
		{
			List<Tuple<string, List<Tuple<int, int>>>> bids = await GetAllBidsOrdered();
			if (!bids.Any())
				return null;
			(string winningItem, List<Tuple<int, int>> winningBids) = bids[0];
			int secondBid = 0;
			if (bids.Count > 1)
			{
				List<Tuple<int, int>> secondItemBids = bids[1].Item2;
				secondBid = secondItemBids.Sum(x => x.Item2);
			}

			int totalBid = winningBids.Sum(x => x.Item2);
			int overpaid = Math.Max(0, totalBid - secondBid - 1);
			int totalCharge = totalBid - overpaid;
			
			List<Tuple<int, int>> moneyOwed = (from userAmountTuple in winningBids.OrderByDescending(x => x.Item2) let percentage = userAmountTuple.Item2 / (double) totalBid select new Tuple<int, int>(userAmountTuple.Item1, (int) Math.Ceiling(totalCharge * percentage))).ToList();

			overpaid = moneyOwed.Sum(x => x.Item2) - totalCharge;
			List<Tuple<int, int>> array = new List<Tuple<int, int>>();
			array.AddRange(moneyOwed);
			if (discountLatter) array.Reverse();
			for (int i = overpaid; i <= 0;)
			{
				foreach (Tuple<int, int> user in array)
				{
					Tuple<int, int> temp = moneyOwed.First(x => x.Item1 == user.Item1);
					Tuple<int, int> temp2 = new Tuple<int, int>(temp.Item1, temp.Item2 - 1);
					moneyOwed[moneyOwed.IndexOf(temp)] = temp2;
					i--;
					if (i == 0) break;
				}
			}

			return new Dictionary<string, object>
			{
				{ "item", winningItem },
				{ "total_bid", totalBid },
				{ "total_charge", totalCharge },
				{ "money_owed", moneyOwed }
			};
		}
	}
}
