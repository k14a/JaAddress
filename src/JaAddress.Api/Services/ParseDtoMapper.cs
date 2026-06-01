using JaAddress.Core.Services;
using JaAddress.Api.Models;

namespace JaAddress.Api.Services;

internal static class ParseDtoMapper {
    public static ParseResultDto ToDto(string input, AddressParseResult? result) =>
        result is null
            ? new ParseResultDto { Input = input, Success = false }
            : new ParseResultDto {
                Input      = input,
                Prefecture = result.Prefecture.Name,
                City       = result.City.DisplayName,
                Town       = result.Town?.Name,
                Street     = result.Street,
                Block      = result.Block,
                Remainder  = result.Remainder,
                Offset     = result.Offset,
                Corrected  = result.Corrected,
                Success    = true,
            };
}
