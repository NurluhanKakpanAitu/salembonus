namespace SalemBonus.Application.Kato.Dtos;

/// <summary>КАТО тармағы. HasChildren төменгі деңгей бар-жоғын көрсетеді.</summary>
public record KatoNodeDto(string Code, string Name, int Level, bool HasChildren);

/// <summary>Іздеу нәтижесі: елді мекен және оның толық жолы.</summary>
public record KatoMatchDto(string Code, string Name, int Level, string Path);
