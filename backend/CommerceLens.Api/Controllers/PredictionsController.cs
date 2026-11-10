using CommerceLens.Api.DTOs;
using CommerceLens.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommerceLens.Api.Controllers;

[ApiController]
[Route("api/model")]
public class ModelController(ClickPredictionService model) : ControllerBase
{
    [HttpGet("status")]
    public Task<ModelStatusDto> Status(CancellationToken token) => model.StatusAsync(token);

    [HttpPost("train")]
    public Task<ModelMetrics> Train(CancellationToken token) => model.TrainAsync(token);
}

[ApiController]
[Route("api/predictions")]
public class PredictionsController(ClickPredictionService model) : ControllerBase
{
    [HttpPost]
    public Task<PredictionDto> Predict(PredictionRequest request, CancellationToken token) => model.PredictAsync(request, token);
}
