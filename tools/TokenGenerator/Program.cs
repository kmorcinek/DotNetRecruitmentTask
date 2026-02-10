using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

string GenerateJwtToken(string issuer, string audience, string[] roles, string userId = "local-dev-user")
{
    var secret = "your-super-secret-key-min-32-chars-long-for-security";
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, userId),
        new Claim(ClaimTypes.NameIdentifier, userId)
    };

    foreach (var role in roles)
    {
        claims.Add(new Claim(ClaimTypes.Role, role));
    }

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.UtcNow.AddHours(24),
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}

Console.WriteLine("=== JWT Tokens for Local Development ===\n");

// ProductService tokens
var productReadWrite = GenerateJwtToken("ProductService", "ProductService", new[] { "read", "write" });
Console.WriteLine("ProductService (read + write roles):");
Console.WriteLine(productReadWrite);
Console.WriteLine();

// InventoryService tokens
var inventoryReadWrite = GenerateJwtToken("InventoryService", "InventoryService", new[] { "read", "write" });
Console.WriteLine("InventoryService (read + write roles):");
Console.WriteLine(inventoryReadWrite);
Console.WriteLine();

Console.WriteLine("=== Usage ===");
Console.WriteLine("ProductService:  curl -H \"Authorization: Bearer <TOKEN>\" http://localhost:5044/products");
Console.WriteLine("InventoryService: curl -H \"Authorization: Bearer <TOKEN>\" http://localhost:5132/inventory");
