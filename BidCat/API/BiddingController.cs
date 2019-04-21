using ApiListener;
using System;
using System.Collections.Generic;
using System.Linq;
using BidCat.Banks;
using Newtonsoft.Json;

namespace BidCat.API
{
	public class BiddingController : ApiProvider
	{
		Dictionary<int, AbstractBank> banks = new Dictionary<int, AbstractBank>();
		Dictionary<int, Auction> auctions = new Dictionary<int, Auction>();
		public override IEnumerable<ApiCommand> Commands => new List<ApiCommand>
		{
			//The Auction ID must be first in the URL when it's needed, same for the Bank ID
			new ApiCommand("RegisterBank", args => args.Count() == 3 ? CreateBank(args.ToArray()[0], args.ToArray()[1], args.ToArray()[2]) : args.Count() == 1 ? CreateBank(JsonConvert.DeserializeObject<List<string>>(args.First())) : throw new ApiError("Incorrect number of argument, expected 3"), new List<ApiParameter>{ new ApiParameter("Users Collection Name", "string"), new ApiParameter("Transactions Collection Name", "string"), new ApiParameter("Currency Field Name", "string") }, "Registers a bank system. Returns the ID of the bank."),
			new ApiCommand("RegisterAuction", args => args.Any() ? RegisterAuction(int.Parse(args.First())) : throw new ApiError("Bank ID was not specified"), new List<ApiParameter> { new ApiParameter("Bank ID") }, "Registers a new auction."),
			new ApiCommand("GetReservedMoneyAuction", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetReservedMoney(args.Skip(1).Count() == 1 ? JsonConvert.DeserializeObject<IEnumerable<int>>(args.Skip(1).First()) : args.Skip(1).Any() ? args.Skip(1).Select(int.Parse) : throw new ApiError("User ID was not specified")).Wait(), new List<ApiParameter>{ new ApiParameter("Auction ID"), new ApiParameter("User Id") }, "Finds the total reserved money for a given user."),
			new ApiCommand("Clear", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).Clear().Wait(), new List<ApiParameter> { new ApiParameter("Auction ID") }, "Clears all bids. This counts as ending the round of bidding for cooldowns, and will apply any cooldown changes."),
			new ApiCommand("GetAllBidsOrdered", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetAllBidsOrdered().Wait(), new List<ApiParameter>{ new ApiParameter("Auction ID") }, "Returns all bids as List<Tuple<string, List<Tuple<int, int>>>>, ordered by ranking (first=winner)"),
			new ApiCommand("GetBidsForUser", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetBidsForUser(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("User ID was not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("User ID") }, "Gets all bids on every lot for the given user"),
			new ApiCommand("GetBidsForUsers", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetBidsForUsers(args.Skip(1).Count() == 1 ? JsonConvert.DeserializeObject<IEnumerable<int>>(args.Skip(1).First()) : args.Skip(1).Any() ? args.Skip(1).Select(int.Parse) : throw new ApiError("User ID was not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("User IDs", "IEnumerable<int>") }, "Gets all bids on every lot for the given users"),
			new ApiCommand("PlaceBid", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).PlaceBid(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("User ID was not specified"), args.Skip(2).Any() ? args.Skip(2).First() : throw new ApiError("Lot ID was not specified"), args.Skip(3).Any() ? int.Parse(args.Skip(3).First()) : throw new ApiError("Amount was not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("User ID"), new ApiParameter("Lot ID", "string"), new ApiParameter("Amount") }, "Places a bid on the specified lot"),
			new ApiCommand("PlaceBids", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).PlaceBids(args.Skip(1).Any() ? JsonConvert.DeserializeObject<List<Tuple<int, string, int>>>(args.Skip(1).First()) : throw new ApiError("Bids were not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("Bids", "List<Tuple<int, string, int>>")}, "Places bids for multiple users, the first item of each tuple is the User ID, the second is the Lot ID, the third is the Amount"),
			new ApiCommand("GetWinner", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetWinner(args.Skip(1).Any() ? bool.Parse(args.Skip(1).First()) : false).Wait(), new List<ApiParameter>{ new ApiParameter("Discount Later bidders", "bool", true) }, "Gets the winner of the given auction"),
			new ApiCommand("ReplaceBid", args =>
			{
				if (!args.Any()) throw new ApiError("Auction ID was not specified");
				Auction auction = GetAuction(int.Parse(args.First()));
				if (bool.TryParse(args.Skip(1).First(), out bool result))
				{
					if (!args.Skip(2).Any())
						throw new ApiError("User ID was not specified");
					if (!args.Skip(3).Any())
						throw new ApiError("Item ID was not specified");
					if (!args.Skip(4).Any())
						throw new ApiError("Amount was not specified");
					auction.ReplaceBid(int.Parse(args.Skip(2).First()), args.Skip(3).First(),
						int.Parse(args.Skip(4).First()), result).Wait();
				}
				else
				{
					if (!args.Skip(1).Any())
						throw new ApiError("User ID was not specified");
					if (!args.Skip(2).Any())
						throw new ApiError("Item ID was not specified");
					if (!args.Skip(3).Any())
						throw new ApiError("Amount was not specified");
					auction.ReplaceBid(int.Parse(args.Skip(1).First()), args.Skip(2).First(),
						int.Parse(args.Skip(3).First())).Wait();
				}
			}, new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("Allow Visible Lowering", "bool", true), new ApiParameter("User ID"), new ApiParameter("Item ID", "string"), new ApiParameter("Amount") }, "Replaces a user's bid with a different one"),
			new ApiCommand("ReplaceBids", args =>
			{
				if (!args.Any()) throw new ApiError("Auction ID was not specified");
				Auction auction = GetAuction(int.Parse(args.First()));
				if (bool.TryParse(args.Skip(1).First(), out bool result))
				{
					List<Tuple<int, string, int>> bids =
						JsonConvert.DeserializeObject<List<Tuple<int, string, int>>>(args.Skip(2).First());
					auction.ReplaceBids(bids, result).Wait();
				}
				else
				{
					List<Tuple<int, string, int>> bids =
						JsonConvert.DeserializeObject<List<Tuple<int, string, int>>>(args.Skip(1).First());
					auction.ReplaceBids(bids).Wait();
				}
			}, new List<ApiParameter>{ new ApiParameter("Auction ID"), new ApiParameter("Allow Visible Lowering", "bool", true), new ApiParameter("Bids", "List<Tuple<int, string, int>>") }, "Replaces many user's bids with different ones, the first item of the tuple is the user ID, the second is the lot ID, and the third is the amount"),
			new ApiCommand("IncreaseBid", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).IncreaseBid(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("User ID was not specified"), args.Skip(2).Any() ? args.Skip(2).First() : throw new ApiError("Lot ID was not specified"), args.Skip(3).Any() ? int.Parse(args.Skip(3).First()) : throw new ApiError("Amount was not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("User ID"), new ApiParameter("Lot ID", "string"), new ApiParameter("Amount") }, "Increase a bid on the specified lot"),
			new ApiCommand("IncreaseBids", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).IncreaseBids(args.Skip(1).Any() ? JsonConvert.DeserializeObject<List<Tuple<int, string, int>>>(args.Skip(1).First()) : throw new ApiError("Bids were not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("Bids", "List<Tuple<int, string, int>>")}, "Places bids for multiple users, the first item of each tuple is the User ID, the second is the Lot ID, the third is the Amount to increase by"),
			new ApiCommand("RemoveBid", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).RemoveBid(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("User ID was not specified"), args.Skip(2).Any() ? args.Skip(2).First() : throw new ApiError("Lot ID was not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("User ID"), new ApiParameter("Lot ID", "string")}, "Removes a user's bid"),
			new ApiCommand("RemoveBids", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).RemoveBids(args.Skip(1).Any() ? JsonConvert.DeserializeObject<List<Tuple<int, string>>>(args.Skip(1).First()) : throw new ApiError("Bids were not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("Bids", "List<Tuple<int, string>>")}, "Removes bids for multiple users, the first item of each tuple is the User ID, the second is the Lot ID"),
			new ApiCommand("GetBidsForItem", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetBidsForItem(args.Skip(1).Any() ? args.Skip(1).First() : throw new ApiError("Lot ID was not specified")).Wait(), new List<ApiParameter>{ new ApiParameter("Auction ID"), new ApiParameter("Lot ID", "string") }, "Gets all bids for a specified lot"),
			new ApiCommand("GetBidsForItems", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetBidsForItems(args.Skip(1).Any() ? JsonConvert.DeserializeObject<List<string>>(args.Skip(1).First()) : throw new ApiError("Lots were not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("Lots", "List<string>")}, "Gets all bids for multiple lots."),
			new ApiCommand("GetAllBids", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetAllBids().Wait(), new List<ApiParameter> { new ApiParameter("Auction ID") }, "Returns all bids for a specified Auction"),
			new ApiCommand("MakeTransaction", args => GetBank(args.Any() ? int.Parse(args.First()) : throw new ApiError("Bank ID was not specified")).MakeTransaction(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("User ID was not specified"), args.Skip(2).Any() ? int.Parse(args.Skip(2).First()) : throw new ApiError("Amount was not specified"), args.Skip(3).Any() ? JsonConvert.DeserializeObject<Dictionary<string, object>>(args.Skip(3).First()) : new Dictionary<string, object>()).Wait(), new List<ApiParameter>{ new ApiParameter("Bank ID"), new ApiParameter("User ID"), new ApiParameter("Amount"), new ApiParameter("Extra", "Dictionary<string, object>", true) }, "Makes a transaction for the specified user"),
			new ApiCommand("MakeTransactions", args => GetBank(args.Any() ? int.Parse(args.First()) : throw new ApiError("Bank ID was not specified")).MakeTransactions(args.Skip(1).Any() ? JsonConvert.DeserializeObject<List<Tuple<int, int, Dictionary<string, object>>>>(args.Skip(1).First()) : throw new ApiError("Transactions were not specified")).Wait(), new List<ApiParameter>{ new ApiParameter("Bank ID"), new ApiParameter("Transactions", "List<Tuple<int, int, Dictionary<string, object>>>") }, "Makes many transactions for the specified users, the first item of the tuple is the User ID, the second is the amount to change by, and the third is any additional records to include"),
			new ApiCommand("GetReservedMoneyBank", args => GetBank(args.Any() ? int.Parse(args.First()) : throw new ApiError("Bank ID was not specified")).GetReservedMoney(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("User ID was not specified")).Wait(), new List<ApiParameter>{ new ApiParameter("Bank ID"), new ApiParameter("User ID") }, "Gets the total reserved money for the specified user"),
			new ApiCommand("GetAvailableMoneyBank", args => GetBank(args.Any() ? int.Parse(args.First()) : throw new ApiError("Bank ID was not specified")).GetAvailableMoney(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("User ID was not specified")).Wait(), new List<ApiParameter>{ new ApiParameter("Bank ID"), new ApiParameter("User ID") }, "Gets the total available money for the specified user"),
			new ApiCommand("RemoveAuction", args => RemoveAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")), new List<ApiParameter>{ new ApiParameter("Auction ID") }, "Removes an Auction."),
			new ApiCommand("RemoveBank", args => RemoveBank(args.Any() ? int.Parse(args.First()) : throw new ApiError("Bank ID was not specified")), new List<ApiParameter>{ new ApiParameter("Bank ID") }, "Removes a bank."),
			new ApiCommand("RegisterTimeSoftCooldown", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).RegisterSoftCooldown(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("Cooldown length was not specified"), args.Skip(2).Any() ? int.Parse(args.Skip(2).First()) : throw new ApiError("Increment Length was not specified"), args.Skip(3).Any() ? int.Parse(args.Skip(3).First()) : throw new ApiError("Base minimum bid was not specified"), args.Skip(4).Any() ? int.Parse(args.Skip(4).First()) : throw new ApiError("Increment amount was not specified"), false).Wait(), new List<ApiParameter>{ new ApiParameter("Auction ID"), new ApiParameter("Cooldown Length"), new ApiParameter("Increment Length"), new ApiParameter("Base Minimum Bid"), new ApiParameter("Increment Amount") }, "Registers a soft cooldown for the given auction, lengths should be given in milliseconds."),
			new ApiCommand("RegisterLotSoftCooldown", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).RegisterSoftCooldown(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("Cooldown length was not specified"), args.Skip(2).Any() ? int.Parse(args.Skip(2).First()) : throw new ApiError("Increment Length was not specified"), args.Skip(3).Any() ? int.Parse(args.Skip(3).First()) : throw new ApiError("Base minimum bid was not specified"), args.Skip(4).Any() ? int.Parse(args.Skip(4).First()) : throw new ApiError("Increment amount was not specified"), true).Wait(), new List<ApiParameter>{ new ApiParameter("Auction ID"), new ApiParameter("Cooldown Length"), new ApiParameter("Increment Length"), new ApiParameter("Base Minimum Bid"), new ApiParameter("Increment Amount") }, "Registers a soft cooldown for the given auction, lengths should be given in amounts of lots."),
			new ApiCommand("DeregisterSoftCooldown", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).DeregisterSoftCooldown().Wait(), new List<ApiParameter>{ new ApiParameter("Auction ID") }, "De-registers a soft cooldown for the given auction"),
			new ApiCommand("RegisterTimeHardCooldown", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).RegisterHardCooldown(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("Cooldown length was not specified"), false).Wait(), new List<ApiParameter>{ new ApiParameter("Auction ID"), new ApiParameter("Cooldown Length") }, "Registers a hard cooldown for the given auction, lengths should be given in milliseconds."),
			new ApiCommand("RegisterLotHardCooldown", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).RegisterHardCooldown(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("Cooldown length was not specified"), true).Wait(), new List<ApiParameter>{ new ApiParameter("Auction ID"), new ApiParameter("Cooldown Length") }, "Registers a hard cooldown for the given auction, lengths should be given in amounts of lots."),
			new ApiCommand("DeregisterHardCooldown", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).DeregisterHardCooldown().Wait(), new List<ApiParameter>{ new ApiParameter("Auction ID") }, "De-registers a hard cooldown for the given auction"),
			new ApiCommand("GetCooldown", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetCooldown(args.Skip(1).Any() ? args.Skip(1).First() : throw new ApiError("Lot ID was not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("Lot ID", "string") }, "Gets the cooldown for a specified lot"),
			new ApiCommand("GetCooldowns", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetCooldowns(args.Skip(1).Any() ? JsonConvert.DeserializeObject<List<string>>(args.Skip(1).First()) : throw new ApiError("Lot IDs were not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("Lot IDs", "IEnumerable<string>") }, "Gets the cooldowns for multiple lots")
		};

		public BiddingController(Action<ApiLogMessage> logger)
			: base(logger)
		{ }

		private int CreateBank(List<string> parameters) =>
			CreateBank(parameters[0], parameters[1], parameters[2]);

		private int CreateBank(string usersCollectionName, string transactionsCollectionName, string fieldName)
		{
			Random random = new Random();
			int randInt = random.Next();
			while (banks.ContainsKey(randInt))
				randInt = random.Next();
			AbstractBank bank = null;
			switch (Listener.Config.StorageType)
			{
				case StorageType.Memory:
					bank = new MemoryBank(Log);
					break;
				case StorageType.Mongo:
					bank = new MongoBank(Log, usersCollectionName, transactionsCollectionName, fieldName);
					break;
				case StorageType.Postgres:
					bank = new PostgresBank(Log);
					break;
			}
			banks.Add(randInt, bank);
			return randInt;
		}

		private void RemoveBank(int BankID)
		{
			if (!banks.ContainsKey(BankID))
				throw new ApiError("Bank with the given ID does not exist");
			AbstractBank bank = GetBank(BankID);
			if (auctions.Any(x => x.Value.bank == bank))
				throw new ApiError("Auctions which reference the given bank still exist, please remove them before attempting to remove the bank");
			banks.Remove(BankID);
		}

		private int RegisterAuction(int bankId)
		{
			Random random = new Random();
			int randInt = random.Next();
			while (auctions.ContainsKey(randInt))
				randInt = random.Next();
			if (!banks.ContainsKey(bankId))
				throw new ApiError("Bank with given ID does not exist");
			auctions.Add(randInt, new Auction(Log, banks[bankId]));
			return randInt;
		}

		private void RemoveAuction(int auctionId)
		{
			if (!auctions.ContainsKey(auctionId))
				throw new ApiError("Auction with give ID does not exist");
			auctions.Remove(auctionId);
		}

		private Auction GetAuction(int auctionID)
		{
			if (!auctions.ContainsKey(auctionID))
				throw new ApiError("Auction with given ID does not exist");
			return auctions[auctionID];
		}

		private AbstractBank GetBank(int bankID)
		{
			if (!banks.ContainsKey(bankID))
				throw new ApiError("Bank with given ID does not exist");
			return banks[bankID];
		}
	}
}
