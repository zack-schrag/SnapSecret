using Moq;
using SnapSecret.Application;
using SnapSecret.Domain;
using System;
using System.Threading.Tasks;
using Xunit;

namespace SnapSecret.Application.Tests
{
    public class SnapSecretBusinessLogicTests
    {
        private readonly Mock<ISecretsProvider> _mockSecretsProvider;

        public SnapSecretBusinessLogicTests()
        {
            _mockSecretsProvider = new Mock<ISecretsProvider>();
        }

        [Fact]
        public async Task ShouldBeAbleToSubmitASecret()
        {
            _mockSecretsProvider
                .Setup(mock => mock.SetSecretAsync(It.IsAny<IShareableTextSecret>()))
                .ReturnsAsync((Guid.NewGuid().ToString(), default));

            var service = new SnapSecretBusinessLogic(_mockSecretsProvider.Object);

            var result = await service.SubmitSecretAsync(new ShareableTextSecret("foo"));

            Assert.NotNull(result.Item1);
            Assert.Null(result.Item2);
        }

        [Fact]
        public async Task ShouldPassThroughErrorFromProvider()
        {
            var userMessage = "Some error";
            var errorType = SnapSecretErrorType.ProviderRequestError;

            var error = new SnapSecretError(errorType)
                .WithUserMessage(userMessage);

            _mockSecretsProvider
                .Setup(mock => mock.SetSecretAsync(It.IsAny<IShareableTextSecret>()))
                .ReturnsAsync((default, error));

            var service = new SnapSecretBusinessLogic(_mockSecretsProvider.Object);

            var result = await service.SubmitSecretAsync(new ShareableTextSecret("foo"));

            Assert.Null(result.Item1);
            Assert.NotNull(result.Item2);
            Assert.Equal(result.Item2?.UserMessage, userMessage);
            Assert.Equal(result.Item2?.ErrorType, errorType);
        }

        [Fact]
        public async Task ShouldBeAbleToAccessSecret()
        {
            var expectedSecret = new ShareableTextSecret("foo");
            var secretId = Guid.NewGuid();

            _mockSecretsProvider
                .Setup(mock => mock.GetSecretAsync(secretId.ToString()))
                .ReturnsAsync((expectedSecret, default));

            _mockSecretsProvider
                .Setup(mock => mock.ExpireSecretAsync(secretId.ToString()))
                .ReturnsAsync(default(SnapSecretError));

            var service = new SnapSecretBusinessLogic(_mockSecretsProvider.Object);

            var result = await service.AccessSecretAsync(secretId);

            Assert.NotNull(result.Item1);
            Assert.Null(result.Item2);
            Assert.Equal(result.Item1?.Text, expectedSecret.Text);

            _mockSecretsProvider.Verify(mock => mock.GetSecretAsync(secretId.ToString()), Times.Once);
            _mockSecretsProvider.Verify(mock => mock.ExpireSecretAsync(secretId.ToString()), Times.Once);
        }

        [Fact]
        public async Task ShouldReturnErrorOnGetSecretError()
        {
            var userMessage = "Some error";
            var errorType = SnapSecretErrorType.ProviderRequestError;

            var error = new SnapSecretError(errorType)
                .WithUserMessage(userMessage);

            _mockSecretsProvider
                .Setup(mock => mock.GetSecretAsync(It.IsAny<string>()))
                .ReturnsAsync((default, error));

            var service = new SnapSecretBusinessLogic(_mockSecretsProvider.Object);

            var result = await service.AccessSecretAsync(Guid.NewGuid());

            Assert.Null(result.Item1);
            Assert.NotNull(result.Item2);
            Assert.Equal(result.Item2?.UserMessage, userMessage);
            Assert.Equal(result.Item2?.ErrorType, errorType);

            _mockSecretsProvider.Verify(mock => mock.GetSecretAsync(It.IsAny<string>()), Times.Once);
            _mockSecretsProvider.Verify(mock => mock.ExpireSecretAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ShouldReturnErrorOnExpireSecretError()
        {
            var expectedSecret = new ShareableTextSecret("foo");
            var secretId = Guid.NewGuid();

            var userMessage = "Some error";
            var errorType = SnapSecretErrorType.ProviderRequestError;

            var error = new SnapSecretError(errorType)
                .WithUserMessage(userMessage);

            _mockSecretsProvider
                .Setup(mock => mock.GetSecretAsync(secretId.ToString()))
                .ReturnsAsync((expectedSecret, default));

            _mockSecretsProvider
                .Setup(mock => mock.ExpireSecretAsync(secretId.ToString()))
                .ReturnsAsync(error);

            var service = new SnapSecretBusinessLogic(_mockSecretsProvider.Object);

            var result = await service.AccessSecretAsync(secretId);

            Assert.Null(result.Item1);
            Assert.NotNull(result.Item2);
            Assert.Equal(result.Item2?.UserMessage, userMessage);
            Assert.Equal(result.Item2?.ErrorType, errorType);

            _mockSecretsProvider.Verify(mock => mock.GetSecretAsync(It.IsAny<string>()), Times.Once);
            _mockSecretsProvider.Verify(mock => mock.ExpireSecretAsync(It.IsAny<string>()), Times.Once);
        }
    }
}
