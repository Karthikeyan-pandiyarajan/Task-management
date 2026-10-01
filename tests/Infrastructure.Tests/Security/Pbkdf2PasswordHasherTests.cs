using FluentAssertions;
using Infrastructure.Security;

namespace Infrastructure.Tests.Security;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _sut = new();

    [Fact]
    public void Hash_does_not_contain_the_plain_password()
    {
        _sut.Hash("Secret123").Should().NotContain("Secret123");
    }

    [Fact]
    public void Same_password_hashes_differently_because_of_random_salt()
    {
        _sut.Hash("Secret123").Should().NotBe(_sut.Hash("Secret123"));
    }

    [Fact]
    public void Verify_accepts_correct_password()
    {
        var hash = _sut.Hash("Secret123");

        _sut.Verify("Secret123", hash).Should().BeTrue();
    }

    [Theory]
    [InlineData("secret123")]
    [InlineData("Secret1234")]
    [InlineData("")]
    public void Verify_rejects_wrong_password(string attempt)
    {
        var hash = _sut.Hash("Secret123");

        _sut.Verify(attempt, hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2-SHA512$abc$!!$!!")]
    public void Verify_returns_false_for_malformed_hash(string storedHash)
    {
        _sut.Verify("Secret123", storedHash).Should().BeFalse();
    }
}
