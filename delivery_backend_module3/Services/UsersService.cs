﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using delivery_backend_module3.Configurations;
using delivery_backend_module3.Exceptions;
using delivery_backend_module3.Models;
using delivery_backend_module3.Models.Dtos;
using delivery_backend_module3.Models.Entities;
using delivery_backend_module3.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace delivery_backend_module3.Services;

public class UsersService : IUsersService
{
    private readonly ApplicationDbContext _context;

    public UsersService(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<TokenDto> RegisterUser(UserRegisterModel userRegisterDto)
    {
        userRegisterDto.email = NormalizeAttribute(userRegisterDto.email);

        await CheckUniqueEmail(userRegisterDto);

        var userEntity = new UserEntity
        {
            Id = Guid.NewGuid(),
            FullName = userRegisterDto.fullName,
            Password = userRegisterDto.password,
            Email = userRegisterDto.email,
            Gender = userRegisterDto.gender,
            BirthDate = userRegisterDto.birthDate,
            Address = userRegisterDto.address,
            PhoneNumber = userRegisterDto.phoneNumber
        };

        await _context.Users.AddAsync(userEntity);
        await _context.SaveChangesAsync();
        
        var loginCredentials = new LoginCredentials
        {
            password = userEntity.Password,
            email = userEntity.Email
        };

        return await LoginUser(loginCredentials);
    }

    public async Task<TokenDto> LoginUser(LoginCredentials loginCredentials)
    {
        loginCredentials.email = NormalizeAttribute(loginCredentials.email);

        var identity = await GetIdentity(loginCredentials.email, loginCredentials.password);

        var now = DateTime.UtcNow;

        var jwt = new JwtSecurityToken(
            issuer: JwtConfigurations.Issuer,
            audience: JwtConfigurations.Audience,
            notBefore: now,
            claims: identity.Claims,
            expires: now.AddMinutes(JwtConfigurations.Lifetime),
            signingCredentials: new SigningCredentials(JwtConfigurations.GetSymmetricSecurityKey(),
                SecurityAlgorithms.HmacSha256));

        var encodeJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

        var result = new TokenDto()
        {
            token = encodeJwt
        };

        return result;
    }

    
    
    private static string NormalizeAttribute(string attribute)
    {
        var result = attribute.ToLower();
        result = result.TrimEnd();

        return result;
    }
    
    private async Task CheckUniqueEmail(UserRegisterModel userRegisterDto)
    {
        var checkUniqueEmail = await _context
            .Users
            .Where(x => userRegisterDto.email == x.Email)
            .FirstOrDefaultAsync();

        if (checkUniqueEmail != null)
        {
            throw new UserAlreadyExistException($"Email '{userRegisterDto.email}' is already taken");
        }
    }
    
    private async Task<ClaimsIdentity> GetIdentity(string email, string password)
    {
        var userEntity = await _context
            .Users
            .Where(x => x.Email == email && x.Password == password)
            .FirstOrDefaultAsync();

        if (userEntity == null)
        {
            throw new WrongLoginCredentialsException("Login failed");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimsIdentity.DefaultNameClaimType, userEntity.Id.ToString())
        };

        var claimsIdentity = new ClaimsIdentity
        (
            claims,
            "Token",
            ClaimsIdentity.DefaultNameClaimType,
            "User"
        );

        return claimsIdentity;
    }
}