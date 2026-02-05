using NetworkConfigApp.Core.Models;
using Xunit;

namespace NetworkConfigApp.Tests.Models
{
    /// <summary>
    /// Unit tests for NetworkConfiguration model.
    /// </summary>
    public class NetworkConfigurationTests
    {
        [Fact]
        public void Empty_ReturnsEmptyConfiguration()
        {
            var config = NetworkConfiguration.Empty();

            Assert.Equal(string.Empty, config.IpAddress);
            Assert.Equal(string.Empty, config.SubnetMask);
            Assert.Equal(string.Empty, config.Gateway);
            Assert.Equal(string.Empty, config.Dns1);
            Assert.Equal(string.Empty, config.Dns2);
            Assert.False(config.IsDhcp);
        }

        [Fact]
        public void Dhcp_ReturnsDhcpConfiguration()
        {
            var config = NetworkConfiguration.Dhcp("Test");

            Assert.True(config.IsDhcp);
            Assert.Equal(string.Empty, config.IpAddress);
            Assert.Equal("Test", config.Description);
        }

        [Fact]
        public void Static_ReturnsStaticConfiguration()
        {
            var config = NetworkConfiguration.Static(
                "192.168.1.100",
                "255.255.255.0",
                "192.168.1.1",
                "8.8.8.8",
                "8.8.4.4",
                "Test");

            Assert.False(config.IsDhcp);
            Assert.Equal("192.168.1.100", config.IpAddress);
            Assert.Equal("255.255.255.0", config.SubnetMask);
            Assert.Equal("192.168.1.1", config.Gateway);
            Assert.Equal("8.8.8.8", config.Dns1);
            Assert.Equal("8.8.4.4", config.Dns2);
            Assert.Equal("Test", config.Description);
        }

        [Fact]
        public void WithIpAddress_ReturnsNewConfiguration()
        {
            var original = NetworkConfiguration.Static("192.168.1.100", "255.255.255.0", "192.168.1.1");
            var modified = original.WithIpAddress("192.168.1.200");

            Assert.NotEqual(original, modified);
            Assert.Equal("192.168.1.200", modified.IpAddress);
            Assert.Equal(original.SubnetMask, modified.SubnetMask);
            Assert.Equal(original.Gateway, modified.Gateway);
        }

        [Fact]
        public void WithSubnetMask_ReturnsNewConfiguration()
        {
            var original = NetworkConfiguration.Static("192.168.1.100", "255.255.255.0", "192.168.1.1");
            var modified = original.WithSubnetMask("255.255.0.0");

            Assert.NotEqual(original, modified);
            Assert.Equal("255.255.0.0", modified.SubnetMask);
            Assert.Equal(original.IpAddress, modified.IpAddress);
        }

        [Fact]
        public void WithGateway_ReturnsNewConfiguration()
        {
            var original = NetworkConfiguration.Static("192.168.1.100", "255.255.255.0", "192.168.1.1");
            var modified = original.WithGateway("192.168.1.254");

            Assert.NotEqual(original, modified);
            Assert.Equal("192.168.1.254", modified.Gateway);
        }

        [Fact]
        public void WithDns_ReturnsNewConfiguration()
        {
            var original = NetworkConfiguration.Static("192.168.1.100", "255.255.255.0", "192.168.1.1");
            var modified = original.WithDns("1.1.1.1", "1.0.0.1");

            Assert.NotEqual(original, modified);
            Assert.Equal("1.1.1.1", modified.Dns1);
            Assert.Equal("1.0.0.1", modified.Dns2);
        }

        [Theory]
        [InlineData("255.255.255.0", 24)]
        [InlineData("255.255.0.0", 16)]
        [InlineData("255.0.0.0", 8)]
        [InlineData("255.255.255.128", 25)]
        [InlineData("255.255.255.252", 30)]
        public void GetCidrPrefix_ValidMasks_ReturnsCorrectPrefix(string mask, int expected)
        {
            var config = NetworkConfiguration.Static("192.168.1.100", mask, "192.168.1.1");
            Assert.Equal(expected, config.GetCidrPrefix());
        }

        [Theory]
        [InlineData(24, "255.255.255.0")]
        [InlineData(16, "255.255.0.0")]
        [InlineData(8, "255.0.0.0")]
        [InlineData(25, "255.255.255.128")]
        public void CidrToSubnetMask_ValidPrefixes_ReturnsCorrectMask(int prefix, string expected)
        {
            var result = NetworkConfiguration.CidrToSubnetMask(prefix);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsValidForStatic_WithRequiredFields_ReturnsTrue()
        {
            var config = NetworkConfiguration.Static("192.168.1.100", "255.255.255.0", "192.168.1.1");
            Assert.True(config.IsValidForStatic());
        }

        [Fact]
        public void IsValidForStatic_MissingIp_ReturnsFalse()
        {
            var config = NetworkConfiguration.Static("", "255.255.255.0", "192.168.1.1");
            Assert.False(config.IsValidForStatic());
        }

        [Fact]
        public void IsValidForStatic_MissingSubnet_ReturnsFalse()
        {
            var config = NetworkConfiguration.Static("192.168.1.100", "", "192.168.1.1");
            Assert.False(config.IsValidForStatic());
        }

        [Fact]
        public void ToDisplayString_Dhcp_ReturnsCorrectString()
        {
            var config = NetworkConfiguration.Dhcp();
            Assert.Equal("DHCP (Automatic)", config.ToDisplayString());
        }

        [Fact]
        public void ToDisplayString_Static_ReturnsCorrectString()
        {
            var config = NetworkConfiguration.Static("192.168.1.100", "255.255.255.0", "192.168.1.1", "8.8.8.8");
            var display = config.ToDisplayString();

            Assert.Contains("192.168.1.100", display);
            Assert.Contains("/24", display);
            Assert.Contains("192.168.1.1", display);
            Assert.Contains("8.8.8.8", display);
        }

        [Fact]
        public void Equals_SameConfiguration_ReturnsTrue()
        {
            var config1 = NetworkConfiguration.Static("192.168.1.100", "255.255.255.0", "192.168.1.1");
            var config2 = NetworkConfiguration.Static("192.168.1.100", "255.255.255.0", "192.168.1.1");

            Assert.True(config1.Equals(config2));
        }

        [Fact]
        public void Equals_DifferentConfiguration_ReturnsFalse()
        {
            var config1 = NetworkConfiguration.Static("192.168.1.100", "255.255.255.0", "192.168.1.1");
            var config2 = NetworkConfiguration.Static("192.168.1.200", "255.255.255.0", "192.168.1.1");

            Assert.False(config1.Equals(config2));
        }

        [Fact]
        public void GetHashCode_SameConfiguration_ReturnsSameHash()
        {
            var config1 = NetworkConfiguration.Static("192.168.1.100", "255.255.255.0", "192.168.1.1");
            var config2 = NetworkConfiguration.Static("192.168.1.100", "255.255.255.0", "192.168.1.1");

            Assert.Equal(config1.GetHashCode(), config2.GetHashCode());
        }
    }
}
