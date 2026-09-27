namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Құпиясөз бен PIN-ді бір бағытты хештеу. Ашық мән ешқашан сақталмайды.</summary>
public interface IPasswordHasher
{
    string Hash(string secret);
    bool Verify(string secret, string hash);
}
