namespace BidCat.DataStructs
{
	public struct MoneyOwed
	{
		public int user_id;
		public int amount;

		public MoneyOwed(int user_id, int amount)
		{
			this.user_id = user_id;
			this.amount = amount;
		}

		public MoneyOwed(MutableTuple<int, int> moneyOwed)
		{
			user_id = moneyOwed.Item1;
			amount = moneyOwed.Item2;
		}
	}
}
