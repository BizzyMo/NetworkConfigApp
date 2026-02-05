using NetworkConfigApp.Core.Validators;
using Xunit;

namespace NetworkConfigApp.Tests.Validators
{
    /// <summary>
    /// Unit tests for IP address validation.
    /// </summary>
    public class IpAddressValidatorTests
    {
        [Theory]
        [InlineData("192.168.1.1", true)]
        [InlineData("10.0.0.1", true)]
        [InlineData("172.16.0.1", true)]
        [InlineData("0.0.0.0", true)]
        [InlineData("255.255.255.255", true)]
        [InlineData("8.8.8.8", true)]
        [InlineData("1.1.1.1", true)]
        public void Validate_ValidIpAddresses_ReturnsValid(string ip, bool expectedValid)
        {
            var result = IpAddressValidator.Validate(ip);
            Assert.Equal(expectedValid, result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void Validate_EmptyOrNull_ReturnsInvalid(string ip)
        {
            var result = IpAddressValidator.Validate(ip);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("256.1.1.1")]
        [InlineData("1.256.1.1")]
        [InlineData("1.1.256.1")]
        [InlineData("1.1.1.256")]
        [InlineData("-1.1.1.1")]
        public void Validate_OctetOutOfRange_ReturnsInvalid(string ip)
        {
            var result = IpAddressValidator.Validate(ip);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("192.168.1")]
        [InlineData("192.168.1.1.1")]
        [InlineData("192.168")]
        [InlineData("192")]
        public void Validate_WrongOctetCount_ReturnsInvalid(string ip)
        {
            var result = IpAddressValidator.Validate(ip);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("abc.def.ghi.jkl")]
        [InlineData("192.168.1.a")]
        [InlineData("192.168.1.1a")]
        [InlineData("192.168.1.")]
        [InlineData(".192.168.1.1")]
        public void Validate_InvalidCharacters_ReturnsInvalid(string ip)
        {
            var result = IpAddressValidator.Validate(ip);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("127.1.2.3")]
        public void Validate_LoopbackAddress_ReturnsWarning(string ip)
        {
            var result = IpAddressValidator.Validate(ip);
            Assert.True(result.IsValid);
            Assert.True(result.HasWarning);
            Assert.Contains("Loopback", result.Message);
        }

        [Theory]
        [InlineData("224.0.0.1")]
        [InlineData("239.255.255.255")]
        public void Validate_MulticastAddress_ReturnsWarning(string ip)
        {
            var result = IpAddressValidator.Validate(ip);
            Assert.True(result.IsValid);
            Assert.True(result.HasWarning);
            Assert.Contains("Multicast", result.Message);
        }

        [Theory]
        [InlineData("169.254.1.1")]
        [InlineData("169.254.255.255")]
        public void Validate_LinkLocalAddress_ReturnsWarning(string ip)
        {
            var result = IpAddressValidator.Validate(ip);
            Assert.True(result.IsValid);
            Assert.True(result.HasWarning);
            Assert.Contains("Link-local", result.Message);
        }

        [Fact]
        public void ValidateForStatic_ZeroAddress_ReturnsInvalid()
        {
            var result = IpAddressValidator.ValidateForStatic("0.0.0.0");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateForStatic_BroadcastAddress_ReturnsInvalid()
        {
            var result = IpAddressValidator.ValidateForStatic("255.255.255.255");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidateForStatic_ValidAddress_ReturnsValid()
        {
            var result = IpAddressValidator.ValidateForStatic("192.168.1.100");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ValidateForGateway_EmptyAllowed_ReturnsValid()
        {
            var result = IpAddressValidator.ValidateForGateway("");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ValidateForGateway_ValidAddress_ReturnsValid()
        {
            var result = IpAddressValidator.ValidateForGateway("192.168.1.1");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ValidateForDns_EmptyAllowed_ReturnsValid()
        {
            var result = IpAddressValidator.ValidateForDns("");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ValidateForDns_GoogleDns_ReturnsValid()
        {
            var result = IpAddressValidator.ValidateForDns("8.8.8.8");
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("192.168.1.100", "192.168.1.1", "255.255.255.0", true)]
        [InlineData("192.168.1.100", "192.168.2.1", "255.255.255.0", false)]
        [InlineData("10.0.0.5", "10.0.0.1", "255.255.255.0", true)]
        [InlineData("10.0.0.5", "10.0.1.1", "255.255.255.0", false)]
        public void IsInSameSubnet_VariousInputs_ReturnsExpected(
            string ip, string gateway, string subnet, bool expected)
        {
            var result = IpAddressValidator.IsInSameSubnet(ip, gateway, subnet);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("192.168.1.1", new int[] { 192, 168, 1, 1 })]
        [InlineData("10.0.0.1", new int[] { 10, 0, 0, 1 })]
        [InlineData("255.255.255.255", new int[] { 255, 255, 255, 255 })]
        public void ParseOctets_ValidIp_ReturnsCorrectOctets(string ip, int[] expected)
        {
            var octets = IpAddressValidator.ParseOctets(ip);
            Assert.Equal(expected, octets);
        }

        [Fact]
        public void ParseToUint_ValidIp_ReturnsCorrectValue()
        {
            var ip = "192.168.1.1";
            var result = IpAddressValidator.ParseToUint(ip);
            Assert.Equal(0xC0A80101u, result);
        }

        [Fact]
        public void UintToString_ValidUint_ReturnsCorrectIp()
        {
            var result = IpAddressValidator.UintToString(0xC0A80101u);
            Assert.Equal("192.168.1.1", result);
        }
    }
}
