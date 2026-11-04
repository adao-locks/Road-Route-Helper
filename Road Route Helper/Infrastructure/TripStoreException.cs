namespace Road_Route_Helper.Infrastructure;

/// <summary>The trips file exists but cannot be used. It is left untouched for manual recovery.</summary>
public class TripStoreException(string message, Exception? inner = null) : Exception(message, inner);
