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
			new ApiCommand("RegisterBank", args => args.Count() == 3 ? CreateBank(args.ToArray()[0], args.ToArray()[1], args.ToArray()[2]) : throw new ApiError("Incorrect number of argument, expected 3"), new List<ApiParameter>{ new ApiParameter("Users Collection Name", "string"), new ApiParameter("Transactions Collection Name", "string"), new ApiParameter("Currency Field Name", "string") }, "Registers a bank system. Returns the ID of the bank."),
			new ApiCommand("RegisterAuction", args => args.Any() ? RegisterAuction(int.Parse(args.First())) : throw new ApiError("Bank ID was not specified"), new List<ApiParameter> { new ApiParameter("Bank ID") }, "Registers a new auction."),
			new ApiCommand("GetReservedMoney", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetReservedMoney(args.Skip(1).Count() == 1 ? JsonConvert.DeserializeObject<IEnumerable<int>>(args.Skip(1).First()) : args.Skip(1).Any() ? args.Skip(1).Select(int.Parse) : throw new ApiError("User ID was not specified")).Wait(), new List<ApiParameter>{ new ApiParameter("Auction ID"), new ApiParameter("User Id") }, "Finds the total reserved money for a given user."),
			new ApiCommand("Clear", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).Clear().Wait(), new List<ApiParameter> { new ApiParameter("Auction ID") }, "Clears all bids."),
			new ApiCommand("GetAllBidsOrdered", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetAllBidsOrdered().Wait(), new List<ApiParameter>{ new ApiParameter("Auction ID") }, "Returns all bids as List<Tuple<string, List<Tuple<int, int>>>>, ordered by ranking (first=winner)"),
			new ApiCommand("GetBidsForUser", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetBidsForUser(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("User ID was not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("User ID") }, "Gets all bids on every lot for the given user"),
			new ApiCommand("GetBidsForUsers", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetBidsForUsers(args.Skip(1).Count() == 1 ? JsonConvert.DeserializeObject<IEnumerable<int>>(args.Skip(1).First()) : args.Skip(1).Any() ? args.Skip(1).Select(int.Parse) : throw new ApiError("User ID was not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("User IDs", "IEnumerable<int>") }, "Gets all bids on every lot for the given users"),
			new ApiCommand("PlaceBid", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).PlaceBid(args.Skip(1).Any() ? int.Parse(args.Skip(1).First()) : throw new ApiError("User ID was not specified"), args.Skip(2).Any() ? args.Skip(2).First() : throw new ApiError("Lot ID was not specified"), args.Skip(3).Any() ? int.Parse(args.Skip(3).First()) : throw new ApiError("Amount was not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("User ID"), new ApiParameter("Lot ID", "string"), new ApiParameter("Amount") }, "Places a bid on the specified lot"),
			new ApiCommand("PlaceBids", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).PlaceBids(args.Skip(1).Any() ? JsonConvert.DeserializeObject<List<Tuple<int, string, int>>>(args.Skip(1).First()) : throw new ApiError("Bids were not specified")).Wait(), new List<ApiParameter> { new ApiParameter("Auction ID"), new ApiParameter("Bids", "List<Tuple<int, string, int>>")}, "Places bids for multiple users, the first item of each tuple is the User ID, the second is the Lot ID, the third is the Amount"),
			new ApiCommand("GetWinner", args => GetAuction(args.Any() ? int.Parse(args.First()) : throw new ApiError("Auction ID was not specified")).GetWinner(args.Skip(1).Any() ? bool.Parse(args.Skip(1).First()) : false).Wait(), new List<ApiParameter>{ new ApiParameter("Discount Later bidders", "bool", true) }, "Gets the winner of the given auction")
		};

		public BiddingController(Action<ApiLogMessage> logger)
			: base(logger)
		{ }

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

		private Auction GetAuction(int auctionID)
		{
			if (!auctions.ContainsKey(auctionID))
				throw new ApiError("Auction with given ID does not exist");
			return auctions[auctionID];
		}
	}
}
