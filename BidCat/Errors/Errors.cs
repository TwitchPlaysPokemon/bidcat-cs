using ApiListener;

namespace BidCat
{
	/// <summary>
	/// Raised when the account is not found in the db.
	/// </summary>
	public class AccountNotFoundError : ApiError
	{
		public AccountNotFoundError(string message = null)
			: base(message) { }
	}

	/// <summary>
	/// Base exception for all bidding errors.
	/// </summary>
	public class BiddingError : ApiError
	{
		public BiddingError(string message = null)
			: base(message) { }
	}

	/// <summary>
	/// Raised when a bid fails due to not enough availalbe money.
	/// </summary>
	public class InsufficientMoneyError : BiddingError
	{
		public InsufficientMoneyError(string message = null)
			: base(message) { }
	}

	/// <summary>
	/// Raised when a bit fails due to a previous bid on that item already existing.
	/// </summary>
	public class AlreadyBidError : BiddingError
	{
		public AlreadyBidError(string message = null)
			: base(message) { }
	}

	/// <summary>
	/// Raised when replacing or increasing a bid failed because there was no previous bid.
	/// </summary>
	public class NoExistingBidError : BiddingError
	{
		public NoExistingBidError(string message = null)
			: base(message) { }
	}

	/// <summary>
	/// Raised when replacing a bid would cause the bid to be visibly lowered.
	/// </summary>
	public class VisiblyLoweredError : BiddingError
	{
		public VisiblyLoweredError(string message = null)
			: base(message) { }
	}

	/// <summary>
	/// Raised when the bid is too low as according to the soft cooldown
	/// </summary>
	public class BidTooLowError : BiddingError
	{
		public BidTooLowError(string message = null)
			: base(message) { }
	}
}
