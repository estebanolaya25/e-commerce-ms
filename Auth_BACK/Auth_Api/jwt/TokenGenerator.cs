using CapaDto.Respuestas;
using CapaNegocios.Interfaz.auth.interfaz;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Auth_Api.jwt
{
    public class TokenGenerator : ITokenGenerator
    {
        private readonly IConfiguration _configuration;

        public TokenGenerator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public AccessTokenDto GenerarAccessToken(Guid userId, string email, string nombre, string apellido, IEnumerable<string> roles)
        {
            int minutos = int.TryParse(_configuration["Jwt:JWT_EXPIRE_MINUTES"], out int valor) ? valor : 15;
            DateTime ahora = DateTime.UtcNow;
            DateTime expiracion = ahora.AddMinutes(minutos);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.GivenName, nombre),
                new Claim(JwtRegisteredClaimNames.FamilyName, apellido),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            claims.AddRange(roles.Select(rol => new Claim("role", rol)));

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var jwtSecurityToken = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                notBefore: ahora,
                expires: expiracion,
                signingCredentials: signingCredentials);

            return new AccessTokenDto
            {
                Token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken),
                Expiracion = expiracion
            };
        }

        public string GenerarRefreshToken()
        {
            return Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        }

        public DateTime ObtenerExpiracionRefreshToken()
        {
            int dias = int.TryParse(_configuration["Jwt:RefreshTokenDays"], out int valor) ? valor : 7;
            return DateTime.UtcNow.AddDays(dias);
        }
    }
}
