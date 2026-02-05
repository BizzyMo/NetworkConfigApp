using NetworkConfigApp.Core.Validators;
using Xunit;

namespace NetworkConfigApp.Tests.Validators
{
    /// <summary>
    /// Unit tests for subnet mask validation.
    /// </summary>
    public class SubnetValidatorTests
    {
        [Theory]
        [InlineData("255.255.255.0")]
        [InlineData("255.255.0.0")]
        [InlineData("255.0.0.0")]
        [InlineData("255.255.255.128")]
        [InlineData("255.255.255.192")]
        [InlineData("255.255.255.224")]
        [InlineData("255.255.255.240")]
        [InlineData("255.255.255.248")]
        [InlineData("255.255.255.252")]
        [InlineData("255.255.255.254")]
        [InlineData("255.255.255.255")]
        public void Validate_ValidMasks_ReturnsValid(string mask)
        {
            var result = SubnetValidator.Validate(mask);
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void Validate_EmptyOrNull_ReturnsInvalid(string mask)
        {
            var result = SubnetValidator.Validate(mask);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("255.255.255.1")]   // Not contiguous
        [InlineData("255.255.0.255")]   // Not contiguous
        [InlineData("255.0.255.0")]     // Not contiguous
        [InlineData("192.168.1.0")]     // Not a valid mask
        public void Validate_NonContiguousMasks_ReturnsInvalid(string mask)
        {
            var result = SubnetValidator.Validate(mask);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("256.255.255.0")]
        [InlineData("255.256.255.0")]
        [InlineData("255.255.256.0")]
        [InlineData("255.255.255.256")]
        public void Validate_OctetOutOfRange_ReturnsInvalid(string mask)
        {
            var result = SubnetValidator.Validate(mask);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("/24", "255.255.255.0")]
        [InlineData("/16", "255.255.0.0")]
        [InlineData("/8", "255.0.0.0")]
        [InlineData("/25", "255.255.255.128")]
        [InlineData("/30", "255.255.255.252")]
        [InlineData("/32", "255.255.255.255")]
        [InlineData("/0", "0.0.0.0")]
        public void ValidateCidr_ValidPrefixes_ReturnsCorrectMask(string cidr, string expectedMask)
        {
            var result = SubnetValidator.ValidateCidr(cidr);
            Assert.True(result.IsValid);
            Assert.Equal(expectedMask, result.Value);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("33")]
        [InlineData("100")]
        [InlineData("abc")]
        public void ValidateCidr_InvalidPrefixes_ReturnsInvalid(string cidr)
        {
            var result = SubnetValidator.ValidateCidr(cidr);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData(24, "255.255.255.0")]
        [InlineData(16, "255.255.0.0")]
        [InlineData(8, "255.0.0.0")]
        [InlineData(25, "255.255.255.128")]
        [InlineData(30, "255.255.255.252")]
        [InlineData(32, "255.255.255.255")]
        [InlineData(0, "0.0.0.0")]
        public void CidrToMask_ValidPrefixes_ReturnsCorrectMask(int cidr, string expectedMask)
        {
            var result = SubnetValidator.CidrToMask(cidr);
            Assert.Equal(expectedMask, result);
        }

        [Theory]
        [InlineData("255.255.255.0", 24)]
        [InlineData("255.255.0.0", 16)]
        [InlineData("255.0.0.0", 8)]
        [InlineData("255.255.255.128", 25)]
        [InlineData("255.255.255.252", 30)]
        [InlineData("255.255.255.255", 32)]
        public void MaskToCidr_ValidMasks_ReturnsCorrectPrefix(string mask, int expectedCidr)
        {
            var result = SubnetValidator.MaskToCidr(mask);
            Assert.Equal(expectedCidr, result);
        }

        [Theory]
        [InlineData(24, 254)]
        [InlineData(25, 126)]
        [InlineData(26, 62)]
        [InlineData(27, 30)]
        [InlineData(28, 14)]
        [InlineData(29, 6)]
        [InlineData(30, 2)]
        [InlineData(31, 2)]
        [InlineData(32, 1)]
        public void GetHostCount_VariousPrefixes_ReturnsCorrectCount(int prefix, int expectedHosts)
        {
            var result = SubnetValidator.GetHostCount(prefix);
            Assert.Equal(expectedHosts, result);
        }

        [Theory]
        [InlineData("192.168.1.100", "255.255.255.0", "192.168.1.0")]
        [InlineData("10.0.5.20", "255.255.0.0", "10.0.0.0")]
        [InlineData("172.16.32.50", "255.255.255.128", "172.16.32.0")]
        public void GetNetworkAddress_ValidInputs_ReturnsCorrectNetwork(
            string ip, string mask, string expectedNetwork)
        {
            var result = SubnetValidator.GetNetworkAddress(ip, mask);
            Assert.Equal(expectedNetwork, result);
        }

        [Theory]
        [InlineData("192.168.1.100", "255.255.255.0", "192.168.1.255")]
        [InlineData("10.0.5.20", "255.255.0.0", "10.0.255.255")]
        [InlineData("172.16.32.50", "255.255.255.128", "172.16.32.127")]
        public void GetBroadcastAddress_ValidInputs_ReturnsCorrectBroadcast(
            string ip, string mask, string expectedBroadcast)
        {
            var result = SubnetValidator.GetBroadcastAddress(ip, mask);
            Assert.Equal(expectedBroadcast, result);
        }

        [Fact]
        public void GetCommonMasks_ReturnsNonEmptyList()
        {
            var masks = SubnetValidator.GetCommonMasks();
            Assert.NotEmpty(masks);
            Assert.Contains(masks, m => m.Mask == "255.255.255.0");
        }

        [Fact]
        public void GetDescription_CommonPrefixes_ReturnsDescription()
        {
            var desc24 = SubnetValidator.GetDescription(24);
            Assert.Contains("254", desc24);

            var desc16 = SubnetValidator.GetDescription(16);
            Assert.Contains("Class B", desc16);
        }
    }
}
