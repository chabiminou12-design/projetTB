using Stat.Models.ViewModels;
using System.Threading.Tasks;

namespace Stat.Services
{
    public interface IReportService
    {
        Task<(byte[] FileContents, string FileName)> GenerateOperationalExcelAsync(string situationId);
        Task<(byte[] FileContents, string FileName)> GenerateOperationalPdfAsync(string situationId);
        Task<(byte[] FileContents, string FileName)> GenerateStrategicExcelAsync(string situationId);
        Task<(byte[] FileContents, string FileName)> GenerateStrategicPdfAsync(string situationId);
        Task<(byte[] FileContents, string FileName)> GenerateDriExcelAsync(string situationId);
        Task<(byte[] FileContents, string FileName)> GenerateDriPdfAsync(string situationId);
        // Add these lines inside the interface IReportService
        Task<(byte[] FileContents, string FileName)> GenerateAnalysisOpExcelAsync(List<IndicatorPerformanceViewModel> results, NiveauOpViewModel filters);
        Task<(byte[] FileContents, string FileName)> GenerateAnalysisStratExcelAsync(List<IndicatorStratPerformanceViewModel> results, NiveauStratViewModel filters);
        Task<(byte[] FileContents, string FileName)> GenerateAnalysisDriExcelAsync(List<IndicatorPerformanceDRIViewModel> results, NiveauOpDRIViewModel filters);
    }
}