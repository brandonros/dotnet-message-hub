using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Common.Model;
using Dispatcher.Services;
using System.ComponentModel.DataAnnotations;
using Common.Model.Requests;
using Common.Model.Responses;
using MassTransit;

namespace Dispatcher.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
[Consumes("application/json")]
public class RpcController : ControllerBase
{
    private readonly ILogger<RpcController> _logger;
    private readonly IServiceProvider _serviceProvider;

    public RpcController(
        ILogger<RpcController> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    [HttpPost]
    [Route("/rpc")]
    [ProducesResponseType(typeof(JsonRpcSuccessResponse<object>), 200)]
    [ProducesResponseType(typeof(JsonRpcErrorResponse), 400)]
    [ProducesResponseType(typeof(JsonRpcErrorResponse), 500)]
    public async Task<IActionResult> HandleRpc([FromBody] JsonRpcRequestBase request)
    {
        try
        {
            if (request == null)
            {
                return CreateInvalidRequestResponse("Invalid request");
            }

            if (!ValidateRequest(request, out var errorResponse))
            {
                return errorResponse;
            }

            string methodStr = request.Method.ToString();
            string id = request.Id;

            _logger.LogInformation($"RPC request received: {methodStr} with id {id}");

            // Type-specific handling based on the concrete type
            switch (request)
            {
                case GetUserJsonRpcRequest getUserRequest:
                    return await HandleGetUserRequest(getUserRequest, id);
                default:
                    return CreateMethodNotFoundResponse(id);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing JSON-RPC request");
            return CreateParseErrorResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing RPC request");
            return CreateInternalErrorResponse(null);
        }
    }

    private async Task<IActionResult> HandleGetUserRequest(JsonRpcRequestBase request, string id)
    {
        var queueService = _serviceProvider.GetService<IQueueService<JsonRpcRequest<GetUserRequest>, JsonRpcResponse<GetUserResponse>>>();
        if (queueService == null)
        {
            _logger.LogError("Failed to resolve IQueueService<GetUserRequest, GetUserResponse>");
            return CreateInternalErrorResponse(id);
        }

        if (request is not GetUserJsonRpcRequest getUserRequest)
        {
            return CreateInvalidParamsResponse(id);
        }

        try 
        {
            var response = await queueService.RequestResponse(getUserRequest);
            return Ok(response.Message);
        }
        catch (RequestTimeoutException ex)  // MassTransit specific
        {
            _logger.LogError(ex, "Request timed out");
            return CreateErrorResponse(id, -32000, "Request timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling get user request");
            return CreateInternalErrorResponse(id);
        }
    }

    private bool ValidateRequest(JsonRpcRequestBase request, out IActionResult errorResponse)
    {
        // Validate the request model
        var validationContext = new ValidationContext(request);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
        {
            var error = validationResults.FirstOrDefault();
            errorResponse = CreateInvalidRequestResponse(error?.ErrorMessage);
            return false;
        }

        if (request == null || string.IsNullOrEmpty(request.Method.ToString()))
        {
            errorResponse = CreateInvalidRequestResponse("Invalid Request");
            return false;
        }

        errorResponse = null;
        return true;
    }

    private IActionResult CreateInvalidRequestResponse(string message) =>
        BadRequest(new JsonRpcErrorResponse
        {
            Id = null,
            Error = new JsonRpcError
            {
                Code = -32600,
                Message = message ?? "Invalid Request"
            }
        });

    private IActionResult CreateMethodNotFoundResponse(string id) =>
        BadRequest(new JsonRpcErrorResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = -32601,
                Message = "Method not found"
            }
        });

    private IActionResult CreateInvalidParamsResponse(string id) =>
        BadRequest(new JsonRpcErrorResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = -32602,
                Message = "Invalid params"
            }
        });

    private IActionResult CreateParseErrorResponse() =>
        BadRequest(new JsonRpcErrorResponse
        {
            Id = null,
            Error = new JsonRpcError
            {
                Code = -32700,
                Message = "Parse error"
            }
        });

    private IActionResult CreateInternalErrorResponse(string id) =>
        StatusCode(500, new JsonRpcErrorResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = -32603,
                Message = "Internal error"
            }
        });

    private IActionResult CreateErrorResponse(string id, int code, string message) =>
        BadRequest(new JsonRpcErrorResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message
            }
        });
}
