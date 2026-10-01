using Domain.Entities;

namespace Application.Abstractions;

public interface ITokenGenerator
{
    string Generate(User user);
}
