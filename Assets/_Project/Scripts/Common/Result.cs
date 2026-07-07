namespace Gaffer.Common
{
    /// <summary>
    /// Outcome of an operation that can fail in an expected, recoverable way. Expected failure
    /// returns a <see cref="Result"/> the caller must handle; a broken invariant still fails fast
    /// with a throw (CONVENTIONS §4). Dependency-free so every layer can return it.
    /// </summary>
    public readonly struct Result
    {
        private Result(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public string Error { get; }

        public static Result Success()
        {
            return new Result(true, null);
        }

        public static Result Failure(string error)
        {
            return new Result(false, error);
        }
    }
}
