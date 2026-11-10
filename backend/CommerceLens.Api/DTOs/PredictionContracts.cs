using System.ComponentModel.DataAnnotations;

namespace CommerceLens.Api.DTOs;

public record PredictionRequest(
    [StringLength(100)] string? DeviceType,
    [StringLength(100)] string? AdPosition,
    [StringLength(100)] string? BrowsingHistory,
    [StringLength(100)] string? TimeOfDay);

public record ModelMetrics(DateTime TrainedAt, string Algorithm, string DatasetFingerprint, string ArtifactFileName,
    int TrainRows, int TestRows, int TrainUsers, int TestUsers, int UserOverlap, double HistoricalBaselineCtr,
    double BaselineAccuracy, double Accuracy, double Precision, double Recall, double RocAuc, double F1Score,
    double LogLoss, double TrainingSeconds, string[] Features,
    int TruePositives, int FalsePositives, int TrueNegatives, int FalseNegatives);
public record ModelStatusDto(string Status, bool IsStale, ModelMetrics? Metrics, double CurrentHistoricalCtr);
public record PredictionDto(double Probability, bool PredictedClick, double HistoricalBaselineCtr,
    double ModelDatasetBaselineCtr, DateTime ModelTrainedAt, bool ModelStale,
    string SourceType = "ModelPrediction", string Engine = "ML.NET");
