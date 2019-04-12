using ApiListener;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BidCat.Banks
{
	public class MongoBank : AbstractBank
	{
		private readonly MongoClient Client;
		private readonly IMongoDatabase Db;

		private readonly string UserCollectionName;
		private readonly string TransactionsCollectionName;
		private readonly string FieldName;

		public MongoBank(Action<ApiLogMessage> logger, string userCollectionName = "users", string transactionsCollectionName = "transactions", string fieldName = "money")
			: base(logger)
		{
			Client = new MongoClient(new MongoClientSettings
			{
				ApplicationName = Listener.Config.MongoSettings.ApplicationName,
				Server = new MongoServerAddress(Listener.Config.MongoSettings.Host, (int)Listener.Config.MongoSettings.Port),
				Credential = Listener.Config.MongoSettings.Database == null ||
					Listener.Config.MongoSettings.Username == null || Listener.Config.MongoSettings.Password == null
					? null
					: MongoCredential.CreateCredential(Listener.Config.MongoSettings.Database, Listener.Config.MongoSettings.Username, Listener.Config.MongoSettings.Password),
				UseSsl = true
			});
			Db = Client.GetDatabase(Listener.Config.MongoSettings.Database);
			UserCollectionName = userCollectionName;
			TransactionsCollectionName = transactionsCollectionName;
			FieldName = fieldName;
		}

		protected async override Task<int> GetStoredMoneyValue(int userId)
		{
			IMongoCollection<BsonDocument> result = Db.GetCollection<BsonDocument>(UserCollectionName);
			BsonDocument userdoc = (await result.FindAsync(x => x["_id"] == userId)).FirstOrDefault();
			if (userdoc == null)
				throw new AccountNotFoundError($"No account has been found for {userId}");
			return userdoc[FieldName].AsInt32;
		}

		protected async override Task AdjustStoredMoneyValue(int userId, int change)
		{
			IMongoCollection<BsonDocument> result = Db.GetCollection<BsonDocument>(UserCollectionName);
			BsonDocument userdoc = (await result.FindAsync(x => x["_id"] == userId)).FirstOrDefault();
			if (userdoc == null)
				throw new AccountNotFoundError($"No account has been found for {userId}");
			userdoc[FieldName] = userdoc[FieldName].AsInt32 + change;
			await result.UpdateOneAsync(x => x["_id"] == userId, userdoc);
		}

		protected async override Task RecordTransaction(Dictionary<string, object> records)
		{
			BsonDocument document = records.ToBsonDocument();
			IMongoCollection<BsonDocument> result = Db.GetCollection<BsonDocument>(TransactionsCollectionName);
			await result.InsertOneAsync(document);
		}
	}
}
