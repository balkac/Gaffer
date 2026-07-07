namespace Gaffer.Common
{
    /// <summary>
    /// Outcome of an operation that either yields a <typeparamref name="T"/> or an expected failure
    /// reason. Read <see cref="Value"/> only after checking <see cref="IsSuccess"/>; on failure it is
    /// <c>default</c> (CONVENTIONS §4).
    /// </summary>
    public readonly struct Result<T>
    {
        private Result(bool isSuccess, T value, string error)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public T Value { get; }

        public string Error { get; }

        public static Result<T> Success(T value)
        {
            return new Result<T>(true, value, null);
        }

        public static Result<T> Failure(string error)
        {
            return new Result<T>(false, default, error);
        }
    }
}
