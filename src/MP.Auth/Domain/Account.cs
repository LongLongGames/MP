namespace MP.Auth.Domain;

/// <summary>MP 主账号（全网唯一身份）</summary>
public sealed record Account(Guid Id, short Status, DateTimeOffset CreatedAt);

/// <summary>第三方渠道绑定关系（provider + third_party_id -> account_id）</summary>
public sealed record AccountBinding(
    long Id,
    Guid AccountId,
    string Provider,
    string ThirdPartyId,
    string? ExtraData,
    DateTimeOffset CreatedAt);
