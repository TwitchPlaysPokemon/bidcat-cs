using ApiListener;
using BidCat.Banks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BidCat.API
{
	public class Auction
	{
		// Lot ID, User ID, Amount
		private Dictionary<string, List<Tuple<int, int>>> lotDict = new Dictionary<string, List<Tuple<int, int>>>();

		private List<string> ChangesTracker = new List<string>();

		AbstractBank bank;
		public Auction(Action<ApiLogMessage> logger, AbstractBank bank)
		{
			this.bank = bank;
			this.bank.RegisterReservedMoneyChecker(GetReservedMoney);
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
		public async Task Clear()
		{
			lotDict.Clear();
		}
#pragma warning restore 1998

		public async Task PlaceBid(int user, string item, int amount)
		{
			await HandleBid(user, item, amount);
		}

		public async Task PlaceBids(IEnumerable<Tuple<int, string, int>> bids)
		{
			foreach ((int user, string item, int amount) in bids)
				await PlaceBid(user, item, amount);
		}

		private void UpdateLastChange(string item)
		{
			if (ChangesTracker.Contains(item))
				ChangesTracker.Remove(item);
			ChangesTracker.Add(item);
		}

		private async Task HandleBid(int user, string item, int amount, bool replace = false,
			bool allowVisibleLowering = true)
		{
			if (amount < 1)
			{
				throw new ApiError("Amount must be a number above 0");
			}

			int? previousBid = null;
			if (lotDict.TryGetValue(item, out _))
			{
				previousBid = lotDict[item].FirstOrDefault(x => x.Item1 == user)?.Item2;
			}

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
