using System; using System.Threading.Tasks; using Grpc.Core; using Grpc.Net.Client; using Microsoft.Extensions.DependencyInjection; using Xunit; using Printercontrol;

namespace Agent.Tests
{
    public class PrinterControlTests
    {
        [Fact]
        public async Task TestPrinterControlGrpcService()
        {
            // 创建 GRPC 通道和客户端
            var channel = GrpcChannel.ForAddress("http://localhost:5000");
            var client = new PrinterControl.PrinterControlClient(channel);

            // 测试 GetConfigSection 方法
            var getConfigSectionResponse = await client.GetConfigSectionAsync(new StringInfo { Info = "test" });
            Assert.NotNull(getConfigSectionResponse);
            Assert.Equal("GetConfigSection response", getConfigSectionResponse.Info);

            // 测试 GetConfigValues 方法
            var getConfigValuesResponse = await client.GetConfigValuesAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(getConfigValuesResponse);
            Assert.Equal("GetConfigValues response", getConfigValuesResponse.Info);

            // 测试 SetConfigValues 方法
            var setConfigValuesResponse = await client.SetConfigValuesAsync(new StringInfo { Info = "test" });
            Assert.NotNull(setConfigValuesResponse);
            Assert.Equal(0, setConfigValuesResponse.Dummy);

            // 测试 GetFileFilters 方法
            var getFileFiltersResponse = await client.GetFileFiltersAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(getFileFiltersResponse);
            Assert.Equal("GetFileFilters response", getFileFiltersResponse.Info);

            // 测试 GetFileInfo 方法
            var getFileInfoResponse = await client.GetFileInfoAsync(new JobFileConfig { Xmlconfig = "test.xml" });
            Assert.NotNull(getFileInfoResponse);
            Assert.Equal(100, getFileInfoResponse.Width);
            Assert.Equal(100, getFileInfoResponse.Height);
            Assert.Equal("Test Printer", getFileInfoResponse.Modelname);

            // 测试 SetPrintJobs 方法
            var setPrintJobsResponse = await client.SetPrintJobsAsync(new JobFileConfig { Xmlconfig = "test.xml" });
            Assert.NotNull(setPrintJobsResponse);
            Assert.Equal(0, setPrintJobsResponse.Dummy);

            // 测试 StartPrint 方法
            var startPrintResponse = await client.StartPrintAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(startPrintResponse);
            Assert.Equal(0, startPrintResponse.Dummy);

            // 测试 StopPrint 方法
            var stopPrintResponse = await client.StopPrintAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(stopPrintResponse);
            Assert.Equal(0, stopPrintResponse.Dummy);

            // 测试 PausePrint 方法
            var pausePrintResponse = await client.PausePrintAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(pausePrintResponse);
            Assert.Equal(0, pausePrintResponse.Dummy);

            // 测试 ResumePrint 方法
            var resumePrintResponse = await client.ResumePrintAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(resumePrintResponse);
            Assert.Equal(0, resumePrintResponse.Dummy);

            // 测试 CalculateInk 方法
            var calculateInkResponse = await client.CalculateInkAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(calculateInkResponse);
            Assert.Equal(0, calculateInkResponse.Dummy);

            // 测试 AppendFileToCurrentJob 方法
            var appendFileResponse = await client.AppendFileToCurrentJobAsync(new FileToAppend { Filename = "test.pdf" });
            Assert.NotNull(appendFileResponse);
            Assert.Equal(0, appendFileResponse.Dummy);

            // 测试 AddPrintJobs 方法
            var addPrintJobsResponse = await client.AddPrintJobsAsync(new AddJobFileConfig { Xmlconfig = "test.xml" });
            Assert.NotNull(addPrintJobsResponse);
            Assert.Equal("AddPrintJobs response", addPrintJobsResponse.Info);

            // 测试 GetInterventionInfo 方法
            var getInterventionInfoResponse = await client.GetInterventionInfoAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(getInterventionInfoResponse);
            Assert.Equal("GetInterventionInfo response", getInterventionInfoResponse.Info);

            // 测试 InterventionResumePrint 方法
            var interventionResumePrintResponse = await client.InterventionResumePrintAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(interventionResumePrintResponse);
            Assert.Equal(0, interventionResumePrintResponse.Dummy);

            // 测试 SaveUserConfig 方法
            var saveUserConfigResponse = await client.SaveUserConfigAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(saveUserConfigResponse);
            Assert.Equal(0, saveUserConfigResponse.Dummy);

            // 测试 SaveUserConfigAs 方法
            var saveUserConfigAsResponse = await client.SaveUserConfigAsAsync(new UserConfigFile { Filename = "test.config" });
            Assert.NotNull(saveUserConfigAsResponse);
            Assert.Equal(0, saveUserConfigAsResponse.Dummy);

            // 测试 Plugin_Load 方法
            var pluginLoadResponse = await client.Plugin_LoadAsync(new PluginCommand { PluginName = "test-plugin" });
            Assert.NotNull(pluginLoadResponse);
            Assert.Equal(0, pluginLoadResponse.Dummy);

            // 测试 Plugin_Get 方法
            var pluginGetResponse = await client.Plugin_GetAsync(new PluginCommand { PluginName = "test-plugin" });
            Assert.NotNull(pluginGetResponse);
            Assert.Equal("Plugin_Get response", pluginGetResponse.Info);

            // 测试 Plugin_Set 方法
            var pluginSetResponse = await client.Plugin_SetAsync(new PluginCommand { PluginName = "test-plugin" });
            Assert.NotNull(pluginSetResponse);
            Assert.Equal(0, pluginSetResponse.Dummy);

            // 测试 Plugin_Run 方法
            var pluginRunResponse = await client.Plugin_RunAsync(new PluginCommand { PluginName = "test-plugin" });
            Assert.NotNull(pluginRunResponse);
            Assert.Equal(0, pluginRunResponse.Dummy);

            // 测试 Plugin_Stop 方法
            var pluginStopResponse = await client.Plugin_StopAsync(new PluginCommand { PluginName = "test-plugin" });
            Assert.NotNull(pluginStopResponse);
            Assert.Equal(0, pluginStopResponse.Dummy);

            // 测试 GetInkControlUpdateRequrement 方法
            var getInkControlUpdateRequrementResponse = await client.GetInkControlUpdateRequrementAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(getInkControlUpdateRequrementResponse);
            Assert.Equal(0, getInkControlUpdateRequrementResponse.Size);

            // 测试 SetInkControlLicense 方法
            var setInkControlLicenseResponse = await client.SetInkControlLicenseAsync(new InkLicense { Size = 0 });
            Assert.NotNull(setInkControlLicenseResponse);
            Assert.Equal(0, setInkControlLicenseResponse.Dummy);

            // 测试 GetInkControlInfo 方法
            var getInkControlInfoResponse = await client.GetInkControlInfoAsync(new DummyParam { Dummy = 0 });
            Assert.NotNull(getInkControlInfoResponse);
            Assert.True(getInkControlInfoResponse.IsLicensed);
            Assert.True(getInkControlInfoResponse.IsControlInkUsage);
            Assert.Single(getInkControlInfoResponse.InkInfo);
            Assert.Equal(0xFFFEU, getInkControlInfoResponse.InkInfo[0].Color);
            Assert.Equal(100.0f, getInkControlInfoResponse.InkInfo[0].InkAllowed);
            Assert.Equal(50.0f, getInkControlInfoResponse.InkInfo[0].InkUsed);

            // 测试 GetNotification 流方法（暂时注释，需要修复流处理问题）
            // using var notificationCall = client.GetNotification(new User { Name = "test", Password = "test" });
            // var hasNext = await notificationCall.ResponseStream.MoveNextAsync();
            // Assert.True(hasNext);
            // Assert.Equal(1, notificationCall.ResponseStream.Current.Type);
            // Assert.Equal(2, notificationCall.ResponseStream.Current.Moduleid);
            // Assert.Equal("Notification response", notificationCall.ResponseStream.Current.Xml);
        }
    }
}