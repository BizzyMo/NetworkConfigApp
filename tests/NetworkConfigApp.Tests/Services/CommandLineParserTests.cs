using NetworkConfigApp.Core.Commands;
using Xunit;

namespace NetworkConfigApp.Tests.Services
{
    /// <summary>
    /// Unit tests for command line argument parsing.
    /// </summary>
    public class CommandLineParserTests
    {
        [Fact]
        public void Parse_NoArgs_ReturnsNone()
        {
            var parser = new CommandLineParser(new string[] { });
            Assert.Equal(CommandLineCommand.None, parser.Command);
        }

        [Theory]
        [InlineData("/help")]
        [InlineData("/?")]
        [InlineData("/h")]
        [InlineData("-help")]
        public void Parse_HelpFlag_ReturnsHelpCommand(string arg)
        {
            var parser = new CommandLineParser(new[] { arg });
            Assert.True(parser.IsHelpRequested);
            Assert.Equal(CommandLineCommand.Help, parser.Command);
        }

        [Theory]
        [InlineData("/silent")]
        [InlineData("/s")]
        [InlineData("-silent")]
        public void Parse_SilentFlag_SetsSilentMode(string arg)
        {
            var parser = new CommandLineParser(new[] { "/dhcp", "/adapter:Ethernet", arg });
            Assert.True(parser.IsSilentMode);
        }

        [Fact]
        public void Parse_AdapterFlag_SetsAdapterName()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/dhcp" });
            Assert.Equal("Ethernet", parser.AdapterName);
        }

        [Fact]
        public void Parse_AdapterWithQuotes_SetsAdapterName()
        {
            var parser = new CommandLineParser(new[] { "/adapter:\"Wi-Fi\"", "/dhcp" });
            Assert.Equal("Wi-Fi", parser.AdapterName);
        }

        [Fact]
        public void Parse_DhcpFlag_ReturnsSetDhcpCommand()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/dhcp" });
            Assert.Equal(CommandLineCommand.SetDhcp, parser.Command);
        }

        [Fact]
        public void Parse_StaticWithCidr_ParsesCorrectly()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/static:192.168.1.100/24/192.168.1.1" });

            Assert.Equal(CommandLineCommand.SetStatic, parser.Command);
            Assert.NotNull(parser.StaticConfig);
            Assert.Equal("192.168.1.100", parser.StaticConfig.IpAddress);
            Assert.Equal("255.255.255.0", parser.StaticConfig.SubnetMask);
            Assert.Equal("192.168.1.1", parser.StaticConfig.Gateway);
        }

        [Fact]
        public void Parse_StaticWithSubnetMask_ParsesCorrectly()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/static:192.168.1.100/255.255.255.0/192.168.1.1" });

            Assert.Equal(CommandLineCommand.SetStatic, parser.Command);
            Assert.Equal("192.168.1.100", parser.StaticConfig.IpAddress);
            Assert.Equal("255.255.255.0", parser.StaticConfig.SubnetMask);
            Assert.Equal("192.168.1.1", parser.StaticConfig.Gateway);
        }

        [Fact]
        public void Parse_StaticWithoutGateway_ParsesCorrectly()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/static:192.168.1.100/24" });

            Assert.Equal(CommandLineCommand.SetStatic, parser.Command);
            Assert.Equal("192.168.1.100", parser.StaticConfig.IpAddress);
            Assert.Equal("255.255.255.0", parser.StaticConfig.SubnetMask);
            Assert.Equal(string.Empty, parser.StaticConfig.Gateway);
        }

        [Fact]
        public void Parse_DnsServers_ParsesCorrectly()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/dhcp", "/dns:8.8.8.8,8.8.4.4" });

            Assert.Equal("8.8.8.8", parser.DnsServers.Primary);
            Assert.Equal("8.8.4.4", parser.DnsServers.Secondary);
        }

        [Fact]
        public void Parse_SingleDns_ParsesCorrectly()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/dhcp", "/dns:8.8.8.8" });

            Assert.Equal("8.8.8.8", parser.DnsServers.Primary);
            Assert.Equal(string.Empty, parser.DnsServers.Secondary);
        }

        [Fact]
        public void Parse_PresetFlag_ReturnsApplyPresetCommand()
        {
            var parser = new CommandLineParser(new[] { "/preset:Office" });
            Assert.Equal(CommandLineCommand.ApplyPreset, parser.Command);
            Assert.Equal("Office", parser.PresetName);
        }

        [Fact]
        public void Parse_ReleaseFlag_ReturnsReleaseCommand()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/release" });
            Assert.Equal(CommandLineCommand.Release, parser.Command);
        }

        [Fact]
        public void Parse_RenewFlag_ReturnsRenewCommand()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/renew" });
            Assert.Equal(CommandLineCommand.Renew, parser.Command);
        }

        [Fact]
        public void Parse_ReleaseAndRenew_ReturnsReleaseRenewCommand()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/release", "/renew" });
            Assert.Equal(CommandLineCommand.ReleaseRenew, parser.Command);
        }

        [Fact]
        public void Parse_FlushDns_ReturnsFlushDnsCommand()
        {
            var parser = new CommandLineParser(new[] { "/flushdns" });
            Assert.Equal(CommandLineCommand.FlushDns, parser.Command);
            Assert.True(parser.IsValid); // FlushDns doesn't require adapter
        }

        [Fact]
        public void Parse_Diagnose_ReturnsDiagnoseCommand()
        {
            var parser = new CommandLineParser(new[] { "/diagnose" });
            Assert.Equal(CommandLineCommand.Diagnose, parser.Command);
        }

        [Fact]
        public void Parse_MissingAdapter_AddsError()
        {
            var parser = new CommandLineParser(new[] { "/dhcp" });
            Assert.False(parser.IsValid);
            Assert.Contains(parser.Errors, e => e.Contains("Adapter"));
        }

        [Fact]
        public void Parse_InvalidStaticFormat_AddsError()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/static:invalid" });
            Assert.False(parser.IsValid);
        }

        [Fact]
        public void CreateRequest_ValidParse_ReturnsRequest()
        {
            var parser = new CommandLineParser(new[] { "/adapter:Ethernet", "/dhcp", "/silent" });
            var result = parser.CreateRequest();

            Assert.True(result.IsSuccess);
            Assert.Equal(CommandLineCommand.SetDhcp, result.Value.Command);
            Assert.Equal("Ethernet", result.Value.AdapterName);
            Assert.True(result.Value.IsSilent);
        }

        [Fact]
        public void CreateRequest_InvalidParse_ReturnsFailure()
        {
            var parser = new CommandLineParser(new[] { "/dhcp" }); // Missing adapter
            var result = parser.CreateRequest();

            Assert.False(result.IsSuccess);
            Assert.NotEmpty(result.Error);
        }

        [Fact]
        public void GetHelpText_ReturnsNonEmpty()
        {
            var help = CommandLineParser.GetHelpText();
            Assert.NotEmpty(help);
            Assert.Contains("/adapter", help);
            Assert.Contains("/dhcp", help);
            Assert.Contains("/static", help);
        }
    }
}
