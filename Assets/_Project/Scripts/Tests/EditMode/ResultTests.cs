using Gaffer.Common;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class ResultTests
    {
        [Test]
        public void Success_ByDefault_IsSuccess()
        {
            var result = Result.Success();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.IsFailure, Is.False);
        }

        [Test]
        public void Failure_WithReason_CarriesError()
        {
            var result = Result.Failure("squad is full");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("squad is full"));
        }

        [Test]
        public void SuccessOfT_WithValue_ExposesValue()
        {
            var result = Result<int>.Success(42);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(42));
        }

        [Test]
        public void FailureOfT_WithReason_HasDefaultValue()
        {
            var result = Result<int>.Failure("not found");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Value, Is.EqualTo(0));
        }

        [Test]
        public void Default_IsAFailureWithNoError()
        {
            // Result is a struct, so the zero value exists whether or not anyone means it to: an
            // unassigned field, a `default` in a switch arm, a `new Result[n]`. It has to land on the
            // safe side — a silent success would let an un-set result read as "it worked". The Error
            // being null is the other half: any caller that logs `result.Error` on failure must survive
            // this value.
            Result result = default;

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.Null);
        }

        [Test]
        public void DefaultOfT_IsAFailureWithNoErrorAndNoValue()
        {
            Result<string> result = default;

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.Null);
            Assert.That(result.Value, Is.Null);
        }
    }
}
