using Gaffer.Common;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class ResultTests
    {
        [Test]
        public void Success_ByDefault_IsSuccess()
        {
            Result result = Result.Success();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.IsFailure, Is.False);
        }

        [Test]
        public void Failure_WithReason_CarriesError()
        {
            Result result = Result.Failure("squad is full");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("squad is full"));
        }

        [Test]
        public void SuccessOfT_WithValue_ExposesValue()
        {
            Result<int> result = Result<int>.Success(42);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(42));
        }

        [Test]
        public void FailureOfT_WithReason_HasDefaultValue()
        {
            Result<int> result = Result<int>.Failure("not found");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Value, Is.EqualTo(0));
        }
    }
}
