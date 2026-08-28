namespace DisplayIO.Ads
{
    /// <summary>Failure reported by the SDK.</summary>
    public class DioError
    {
        public string Message { get; }

        public DioError(string message)
        {
            Message = string.IsNullOrEmpty(message) ? "Unknown error" : message;
        }

        public override string ToString() => Message;
    }
}
