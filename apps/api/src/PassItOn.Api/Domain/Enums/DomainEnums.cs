namespace PassItOn.Api.Domain.Enums;

public enum UserRole
{
    User,
    Admin,
}

public enum UserStatus
{
    Active,
    Suspended,
    DeletionRequested,
    Deleted,
}

public enum AuthProvider
{
    Local,
    Google,
    Facebook,
}

public enum ListingStatus
{
    Draft,
    Active,
    Unavailable,
    Archived,
    Removed,
}

public enum ListingCondition
{
    New,
    LikeNew,
    Good,
    Fair,
    Worn,
}

public enum ContactPreference
{
    Email,
    Phone,
    ProfileContact,
    Other,
}

public enum ReportReasonCode
{
    InappropriateContent,
    Spam,
    Duplicate,
    ProhibitedItem,
    SafetyConcern,
    Other,
}

public enum ReportStatus
{
    Open,
    UnderReview,
    Resolved,
    Dismissed,
}

public enum ContentStatus
{
    Draft,
    Published,
    Archived,
}

public enum ContentType
{
    Bulletin,
    Faq,
    Policy,
}

public enum AuditAction
{
    UserSuspended,
    UserRestored,
    UserDeleted,
    ListingRemoved,
    ListingRestored,
    BulletinPublished,
    BulletinUnpublished,
    ContentUpdated,
    ReportResolved,
    ReportDismissed,
}
