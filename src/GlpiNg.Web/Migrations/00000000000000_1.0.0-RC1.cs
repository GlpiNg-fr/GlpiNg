using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class Release_1_0_0_RC1 : Migration
    {
        /// <summary>Date portée par les lignes d'installation, fixe pour que la migration soit reproductible.</summary>
        private static readonly DateTime Seeded = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>127.0.0.1, tel que GLPI le stocke : une adresse IPv4 en entier.</summary>
        private const long Localhost = 2130706433L;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Ipv4RangeStart = table.Column<long>(type: "bigint", nullable: true),
                    Ipv4RangeEnd = table.Column<long>(type: "bigint", nullable: true),
                    Ipv6 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AppTokenDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LogMethod = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    SectionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.SectionName);
                });

            migrationBuilder.CreateTable(
                name: "AssistanceHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistanceHistoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthLdapServers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false),
                    UseSsl = table.Column<bool>(type: "bit", nullable: false),
                    LoginFilter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BaseDn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UseBind = table.Column<bool>(type: "bit", nullable: false),
                    BindDn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BindPasswordProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LoginField = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SyncField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastNameField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstNameField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayNameField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MobileField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TitleField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommentField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SynchronizeGroups = table.Column<bool>(type: "bit", nullable: false),
                    GroupSearchType = table.Column<int>(type: "int", nullable: false),
                    GroupMemberOfField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupFilter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupNameField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupMemberField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UseDnForGroupSearch = table.Column<bool>(type: "bit", nullable: false),
                    UseStartTls = table.Column<bool>(type: "bit", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    PageSize = table.Column<int>(type: "int", nullable: false),
                    MaxResults = table.Column<int>(type: "int", nullable: false),
                    EntityAssignmentTag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthLdapServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthMailServers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Port = table.Column<int>(type: "int", nullable: true),
                    Protocol = table.Column<int>(type: "int", nullable: false),
                    Encryption = table.Column<int>(type: "int", nullable: false),
                    ValidateCertificate = table.Column<bool>(type: "bit", nullable: false),
                    EmailDomain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthMailServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomaticActionRunLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RanAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomaticActionRunLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomaticActionStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FrequencyMinutes = table.Column<int>(type: "int", nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunSuccess = table.Column<bool>(type: "bit", nullable: true),
                    LastRunDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    LastRunError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomaticActionStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CronSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CronSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomAssetDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SystemName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LabelSingular = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LabelPlural = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DocumentsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NotesEnabled = table.Column<bool>(type: "bit", nullable: false),
                    HistoryEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAssetDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DashboardCardPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CardOrder = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardCardPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Entities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Town = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Registration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Longitude = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Altitude = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignmentTag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignmentEmailDomain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TwoFactorAuthRequired = table.Column<bool>(type: "bit", nullable: false),
                    AgentBaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomCssEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CustomCss = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entities_Entities_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Service = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemId = table.Column<int>(type: "int", nullable: true),
                    ItemLabel = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItilFollowups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItilFollowups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItilTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    AssignedUserId = table.Column<int>(type: "int", nullable: true),
                    PlannedStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItilTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LockedFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LockedFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManagementHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementHistoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notepads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorUserId = table.Column<int>(type: "int", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastEditorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notepads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OAuthClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClientSecretHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Grants = table.Column<int>(type: "int", nullable: false),
                    Scopes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RedirectUrisRaw = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpRestrictionsRaw = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true),
                    ParcRight = table.Column<int>(type: "int", nullable: false),
                    AssistanceRight = table.Column<int>(type: "int", nullable: false),
                    GestionRight = table.Column<int>(type: "int", nullable: false),
                    OutilsRight = table.Column<int>(type: "int", nullable: false),
                    AdministrationRight = table.Column<int>(type: "int", nullable: false),
                    ConfigurationRight = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefusedImportLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComputerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Domain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgentIdentifier = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefusedImportLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedSearchOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SavedSearchId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSearchOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TableColumnPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ColumnsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableColumnPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false),
                    SidebarCollapsed = table.Column<bool>(type: "bit", nullable: false),
                    ItemsPerPage = table.Column<int>(type: "int", nullable: false),
                    MacAddressFormat = table.Column<int>(type: "int", nullable: false),
                    DateFormat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timezone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullNameOrder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CsvDelimiter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShowGlpiIds = table.Column<bool>(type: "bit", nullable: true),
                    HistoryOrder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShowCounters = table.Column<bool>(type: "bit", nullable: true),
                    NotifyOnMyChanges = table.Column<bool>(type: "bit", nullable: true),
                    ColorPalette = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HighContrast = table.Column<bool>(type: "bit", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true),
                    AuthSource = table.Column<int>(type: "int", nullable: false),
                    LdapServerId = table.Column<int>(type: "int", nullable: true),
                    ExternalDn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorAuthDisabled = table.Column<bool>(type: "bit", nullable: false),
                    ApiTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ApiTokenDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_AuthLdapServers_LdapServerId",
                        column: x => x.LdapServerId,
                        principalTable: "AuthLdapServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CustomAssetFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomAssetDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    DropdownType = table.Column<int>(type: "int", nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAssetFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomAssetFields_CustomAssetDefinitions_CustomAssetDefinitionId",
                        column: x => x.CustomAssetDefinitionId,
                        principalTable: "CustomAssetDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    AgentUuid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hostname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgentName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgentVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InstalledTasks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnabledTasks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstContactAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastContactAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastContactIp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agents_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Budgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Budgets_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Calendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Calendars_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Clusters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NodeCount = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clusters_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectDefinitions_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComputerRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    LogicalOperator = table.Column<int>(type: "int", nullable: false),
                    AppliesTo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerRules_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Town = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMonths = table.Column<int>(type: "int", nullable: true),
                    NoticeMonths = table.Column<int>(type: "int", nullable: true),
                    Periodicity = table.Column<int>(type: "int", nullable: false),
                    BillingPeriodicity = table.Column<int>(type: "int", nullable: false),
                    Renewal = table.Column<int>(type: "int", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    CustomAssetDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomAssets_CustomAssetDefinitions_CustomAssetDefinitionId",
                        column: x => x.CustomAssetDefinitionId,
                        principalTable: "CustomAssetDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomAssets_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DatabaseInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Engine = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HostName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Port = table.Column<int>(type: "int", nullable: true),
                    Path = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsBackedUp = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatabaseInstances_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeployComputerGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployComputerGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeployComputerGroups_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentMirrorServers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentMirrorServers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentMirrorServers_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentRules_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentUserInteractionTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllowSkip = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentUserInteractionTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentUserInteractionTemplates_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DictionaryRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    LogicalOperator = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    ActionValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DictionaryRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DictionaryRules_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentCategories_DocumentCategories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DocumentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentCategories_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DropdownItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DropdownItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DropdownItems_DropdownItems_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DropdownItems_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EntityHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityHistoryEntries_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntityNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Author = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityNotes_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpenInNewWindow = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalLinks_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FieldUnicityCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RefuseCreation = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnDuplicate = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldUnicityCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldUnicityCriteria_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    VisibleAsRequester = table.Column<bool>(type: "bit", nullable: false),
                    VisibleAsObserver = table.Column<bool>(type: "bit", nullable: false),
                    VisibleAsAssignee = table.Column<bool>(type: "bit", nullable: false),
                    VisibleAsTask = table.Column<bool>(type: "bit", nullable: false),
                    CanBeNotified = table.Column<bool>(type: "bit", nullable: false),
                    CanBeProjectSupervisor = table.Column<bool>(type: "bit", nullable: false),
                    CanContainItems = table.Column<bool>(type: "bit", nullable: false),
                    CanContainUsers = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorAuthRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Groups_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Groups_Groups_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportAssignmentRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    LogicalOperator = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportAssignmentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportAssignmentRules_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportBlacklistEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBlacklistEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBlacklistEntries_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IpRanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true),
                    StartIp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EndIp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IpRanges_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseCategories_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseCategories_KnowledgeBaseCategories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "KnowledgeBaseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationTemplates_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedSearches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    ResultCount = table.Column<int>(type: "int", nullable: false),
                    CountMode = table.Column<int>(type: "int", nullable: false),
                    CriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedSearches_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SnmpCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Community = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuthProtocol = table.Column<int>(type: "int", nullable: false),
                    AuthPassphrase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrivProtocol = table.Column<int>(type: "int", nullable: false),
                    PrivPassphrase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnmpCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SnmpCredentials_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Town = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suppliers_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketCategories_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketCategories_TicketCategories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimeSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeSlots_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Webhooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Event = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    PayloadMode = table.Column<int>(type: "int", nullable: false),
                    CustomPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecretProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SaveResponseBody = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Webhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Webhooks_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProfileHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileHistoryEntries_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserHistoryEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CalendarId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsPerpetual = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarHolidays_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarSegments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CalendarId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSegments_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CalendarId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLevels_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceLevels_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectFileSearchEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pattern = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Recursive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectFileSearchEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectFileSearchEntries_CollectDefinitions_CollectDefinitionId",
                        column: x => x.CollectDefinitionId,
                        principalTable: "CollectDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectRegistryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Hive = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegistryKey = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectRegistryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectRegistryEntries_CollectDefinitions_CollectDefinitionId",
                        column: x => x.CollectDefinitionId,
                        principalTable: "CollectDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    CollectDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    EntryName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectResults_CollectDefinitions_CollectDefinitionId",
                        column: x => x.CollectDefinitionId,
                        principalTable: "CollectDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectWmiEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Moniker = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WmiClass = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectWmiEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectWmiEntries_CollectDefinitions_CollectDefinitionId",
                        column: x => x.CollectDefinitionId,
                        principalTable: "CollectDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerRuleAction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerRuleId = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerRuleAction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerRuleAction_ComputerRules_ComputerRuleId",
                        column: x => x.ComputerRuleId,
                        principalTable: "ComputerRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerRuleCriterion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerRuleId = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerRuleCriterion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerRuleCriterion_ComputerRules_ComputerRuleId",
                        column: x => x.ComputerRuleId,
                        principalTable: "ComputerRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BudgetId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractCosts_Budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "Budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ContractCosts_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomAssetHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomAssetId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAssetHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomAssetHistoryEntries_CustomAssets_CustomAssetId",
                        column: x => x.CustomAssetId,
                        principalTable: "CustomAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomAssetValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomAssetId = table.Column<int>(type: "int", nullable: false),
                    CustomAssetFieldId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAssetValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomAssetValues_CustomAssetFields_CustomAssetFieldId",
                        column: x => x.CustomAssetFieldId,
                        principalTable: "CustomAssetFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomAssetValues_CustomAssets_CustomAssetId",
                        column: x => x.CustomAssetId,
                        principalTable: "CustomAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeployComputerGroupCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeployComputerGroupId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Link = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployComputerGroupCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeployComputerGroupCriteria_DeployComputerGroups_DeployComputerGroupId",
                        column: x => x.DeployComputerGroupId,
                        principalTable: "DeployComputerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeployComputerGroupId = table.Column<int>(type: "int", nullable: true),
                    ActionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChecksJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserInteractionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupersededByPackageId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentPackages_DeployComputerGroups_DeployComputerGroupId",
                        column: x => x.DeployComputerGroupId,
                        principalTable: "DeployComputerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeploymentPackages_DeploymentPackages_SupersededByPackageId",
                        column: x => x.SupersededByPackageId,
                        principalTable: "DeploymentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeploymentPackages_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentRuleCriterion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentRuleId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Link = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentRuleCriterion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentRuleCriterion_DeploymentRules_DeploymentRuleId",
                        column: x => x.DeploymentRuleId,
                        principalTable: "DeploymentRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DictionaryRuleCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DictionaryRuleId = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DictionaryRuleCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DictionaryRuleCriteria_DictionaryRules_DictionaryRuleId",
                        column: x => x.DictionaryRuleId,
                        principalTable: "DictionaryRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sha256 = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuthorUserId = table.Column<int>(type: "int", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true),
                    SourceSha1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsContentMissing = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_DocumentCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "DocumentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EndpointAType = table.Column<int>(type: "int", nullable: false),
                    EndpointAId = table.Column<int>(type: "int", nullable: false),
                    EndpointBType = table.Column<int>(type: "int", nullable: true),
                    EndpointBId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cables_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Cables_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CartridgeItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    TechnicianInCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AlertThreshold = table.Column<int>(type: "int", nullable: false),
                    SnmpLevelOid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartridgeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartridgeItems_DropdownItems_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CartridgeItems_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Computers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperatingSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OsVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HardwareUuid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChassisType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalMemoryMb = table.Column<int>(type: "int", nullable: true),
                    LastLoggedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VmSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Domain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OsKernelVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RemoteManagementId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RemoteManagementType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Site = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Building = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Room = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastInventoryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AgentId = table.Column<int>(type: "int", nullable: true),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Computers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Computers_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Computers_DropdownItems_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Computers_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Computers_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsumableItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    TechnicianInCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AlertThreshold = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumableItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumableItems_DropdownItems_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumableItems_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enclosures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InventoryNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicianInCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SlotCount = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enclosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enclosures_DropdownItems_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enclosures_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Enclosures_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NetworkEquipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InventoryNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Uuid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicianInCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MemoryMb = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkEquipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkEquipments_DropdownItems_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NetworkEquipments_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NetworkEquipments_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PassiveEquipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InventoryNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicianInCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassiveEquipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassiveEquipments_DropdownItems_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PassiveEquipments_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PassiveEquipments_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pdus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InventoryNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicianInCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pdus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pdus_DropdownItems_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pdus_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Pdus_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Phones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InventoryNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Uuid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicianInCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LineCount = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phones_DropdownItems_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Phones_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Phones_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Printers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InventoryNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Uuid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicianInCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitialPageCount = table.Column<int>(type: "int", nullable: true),
                    CurrentPageCount = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SnmpVersion = table.Column<int>(type: "int", nullable: false),
                    SnmpPort = table.Column<int>(type: "int", nullable: false),
                    SnmpCommunity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SnmpUsername = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SnmpAuthProtocol = table.Column<int>(type: "int", nullable: false),
                    SnmpAuthPassphrase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SnmpPrivProtocol = table.Column<int>(type: "int", nullable: false),
                    SnmpPrivPassphrase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Printers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Printers_DropdownItems_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Printers_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Printers_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Racks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InventoryNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicianInCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitCount = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Racks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Racks_DropdownItems_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Racks_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Racks_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SimCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Operator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InventoryNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Msin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pin2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Puk = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Puk2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Voltage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllowVoip = table.Column<bool>(type: "bit", nullable: false),
                    TechnicianInCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimCards_DropdownItems_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimCards_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SimCards_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalLinkItemTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalLinkId = table.Column<int>(type: "int", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalLinkItemTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalLinkItemTypes_ExternalLinks_ExternalLinkId",
                        column: x => x.ExternalLinkId,
                        principalTable: "ExternalLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldUnicityFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FieldUnicityCriterionId = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldUnicityFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldUnicityFields_FieldUnicityCriteria_FieldUnicityCriterionId",
                        column: x => x.FieldUnicityCriterionId,
                        principalTable: "FieldUnicityCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupHistoryEntries_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Author = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupNotes_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupUsers",
                columns: table => new
                {
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupUsers", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_GroupUsers_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportAssignmentRuleAction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportAssignmentRuleId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportAssignmentRuleAction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportAssignmentRuleAction_ImportAssignmentRules_ImportAssignmentRuleId",
                        column: x => x.ImportAssignmentRuleId,
                        principalTable: "ImportAssignmentRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportAssignmentRuleCriterion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportAssignmentRuleId = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportAssignmentRuleCriterion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportAssignmentRuleCriterion_ImportAssignmentRules_ImportAssignmentRuleId",
                        column: x => x.ImportAssignmentRuleId,
                        principalTable: "ImportAssignmentRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    IsFaq = table.Column<bool>(type: "bit", nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    VisibleFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VisibleUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<int>(type: "int", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastEditorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceGlpiId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseArticles_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseArticles_KnowledgeBaseCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "KnowledgeBaseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Event = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NotificationTemplateId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_NotificationTemplates_NotificationTemplateId",
                        column: x => x.NotificationTemplateId,
                        principalTable: "NotificationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Appliances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Environment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appliances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appliances_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Appliances_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appliances_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Certificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DnsName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Issuer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssuedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Certificates_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Certificates_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Certificates_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ContractSuppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractSuppliers_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractSuppliers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Datacenters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Town = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Datacenters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Datacenters_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Datacenters_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Datacenters_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Domains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Domains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Domains_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Domains_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Domains_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PhoneLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhoneLines_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PhoneLines_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhoneLines_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SoftwareLicenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SoftwareName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LicenseKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Seats = table.Column<int>(type: "int", nullable: false),
                    UsedSeats = table.Column<int>(type: "int", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareLicenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftwareLicenses_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SoftwareLicenses_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoftwareLicenses_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupplierContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    ContactId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierContacts_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierContacts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Changes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsPriorityManual = table.Column<bool>(type: "bit", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    AuthorUserId = table.Column<int>(type: "int", nullable: true),
                    AssignedUserId = table.Column<int>(type: "int", nullable: true),
                    AssignedGroupId = table.Column<int>(type: "int", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImpactContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ControlListContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RolloutPlanContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BackoutPlanContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChecklistContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GlobalValidation = table.Column<int>(type: "int", nullable: false),
                    Solution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SolutionType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Changes_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Changes_TicketCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Problems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsPriorityManual = table.Column<bool>(type: "bit", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    AuthorUserId = table.Column<int>(type: "int", nullable: true),
                    AssignedUserId = table.Column<int>(type: "int", nullable: true),
                    AssignedGroupId = table.Column<int>(type: "int", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SymptomContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CauseContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImpactContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Workaround = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Solution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SolutionType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Problems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Problems_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Problems_TicketCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AllowRePreparation = table.Column<bool>(type: "bit", nullable: false),
                    ScheduledStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreparationTimeSlotId = table.Column<int>(type: "int", nullable: true),
                    ExecutionTimeSlotId = table.Column<int>(type: "int", nullable: true),
                    AgentWakeUpIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    AgentWakeUpCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLaunchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentTasks_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeploymentTasks_TimeSlots_ExecutionTimeSlotId",
                        column: x => x.ExecutionTimeSlotId,
                        principalTable: "TimeSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeploymentTasks_TimeSlots_PreparationTimeSlotId",
                        column: x => x.PreparationTimeSlotId,
                        principalTable: "TimeSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    ScheduledStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutionTimeSlotId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLaunchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTasks_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NetworkTasks_TimeSlots_ExecutionTimeSlotId",
                        column: x => x.ExecutionTimeSlotId,
                        principalTable: "TimeSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimeSlotEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimeSlotId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeSlotEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeSlotEntries_TimeSlots_TimeSlotId",
                        column: x => x.TimeSlotId,
                        principalTable: "TimeSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WakeOnLanTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ScheduledStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutionTimeSlotId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLaunchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WakeOnLanTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTasks_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTasks_TimeSlots_ExecutionTimeSlotId",
                        column: x => x.ExecutionTimeSlotId,
                        principalTable: "TimeSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QueuedWebhooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WebhookId = table.Column<int>(type: "int", nullable: true),
                    WebhookName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Event = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecretProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SaveResponseBody = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedWebhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueuedWebhooks_Webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "Webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WebhookHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WebhookId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookHeaders_Webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "Webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLevelAgreements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceLevelId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Target = table.Column<int>(type: "int", nullable: false),
                    DurationValue = table.Column<int>(type: "int", nullable: false),
                    DurationUnit = table.Column<int>(type: "int", nullable: false),
                    EndOfWorkingDay = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLevelAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLevelAgreements_ServiceLevels_ServiceLevelId",
                        column: x => x.ServiceLevelId,
                        principalTable: "ServiceLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentPackageFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentPackageId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sha512 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentPackageFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentPackageFiles_DeploymentPackages_DeploymentPackageId",
                        column: x => x.DeploymentPackageId,
                        principalTable: "DeploymentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentPackageTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentPackageId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentPackageTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentPackageTargets_DeploymentPackages_DeploymentPackageId",
                        column: x => x.DeploymentPackageId,
                        principalTable: "DeploymentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentRuleAction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentRuleId = table.Column<int>(type: "int", nullable: false),
                    PackageId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentRuleAction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentRuleAction_DeploymentPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "DeploymentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentRuleAction_DeploymentRules_DeploymentRuleId",
                        column: x => x.DeploymentRuleId,
                        principalTable: "DeploymentRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentItems_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CableHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CableId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CableHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CableHistoryEntries_Cables_CableId",
                        column: x => x.CableId,
                        principalTable: "Cables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CartridgeItemHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CartridgeItemId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartridgeItemHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartridgeItemHistoryEntries_CartridgeItems_CartridgeItemId",
                        column: x => x.CartridgeItemId,
                        principalTable: "CartridgeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerAntiviruses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Company = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Guid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: true),
                    UpToDate = table.Column<bool>(type: "bit", nullable: true),
                    Expiration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BaseCreationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BaseVersion = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerAntiviruses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerAntiviruses_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerBatteries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Serial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Chemistry = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VoltageMv = table.Column<int>(type: "int", nullable: true),
                    CapacityMwh = table.Column<int>(type: "int", nullable: true),
                    RealCapacityMwh = table.Column<int>(type: "int", nullable: true),
                    ManufactureDate = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerBatteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerBatteries_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Capacity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Serial = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerComponents_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerConnectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Caption = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerConnectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerConnectors_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerHistoryEntries_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerImportHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgentIdentifier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InputValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerImportHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerImportHistories_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerNetworkPorts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MacAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpMask = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpGateway = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpSubnet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpDhcp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mtu = table.Column<int>(type: "int", nullable: true),
                    SpeedMbps = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsVirtual = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerNetworkPorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerNetworkPorts_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerPeripherals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Serial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerPeripherals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerPeripherals_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComputerPeripherals_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ComputerSoftwares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InstallDate = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerSoftwares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerSoftwares_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerVolumes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Partition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MountPoint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalSizeMb = table.Column<long>(type: "bigint", nullable: true),
                    FreeSizeMb = table.Column<long>(type: "bigint", nullable: true),
                    Encryption = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerVolumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerVolumes_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeployComputerGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeployComputerGroupId = table.Column<int>(type: "int", nullable: false),
                    ComputerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployComputerGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeployComputerGroupMembers_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeployComputerGroupMembers_DeployComputerGroups_DeployComputerGroupId",
                        column: x => x.DeployComputerGroupId,
                        principalTable: "DeployComputerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Peripherals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InventoryNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Uuid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Site = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Building = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Room = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicianInCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsGlobalManagement = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComputerId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Peripherals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Peripherals_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Peripherals_DropdownItems_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DropdownItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Peripherals_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsumableItemHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConsumableItemId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumableItemHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumableItemHistoryEntries_ConsumableItems_ConsumableItemId",
                        column: x => x.ConsumableItemId,
                        principalTable: "ConsumableItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Consumables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConsumableItemId = table.Column<int>(type: "int", nullable: false),
                    DateIn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateOut = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consumables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consumables_ConsumableItems_ConsumableItemId",
                        column: x => x.ConsumableItemId,
                        principalTable: "ConsumableItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnclosureHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnclosureId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnclosureHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnclosureHistoryEntries_Enclosures_EnclosureId",
                        column: x => x.EnclosureId,
                        principalTable: "Enclosures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkEquipmentHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkEquipmentId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkEquipmentHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkEquipmentHistoryEntries_NetworkEquipments_NetworkEquipmentId",
                        column: x => x.NetworkEquipmentId,
                        principalTable: "NetworkEquipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PassiveEquipmentHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PassiveEquipmentId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassiveEquipmentHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassiveEquipmentHistoryEntries_PassiveEquipments_PassiveEquipmentId",
                        column: x => x.PassiveEquipmentId,
                        principalTable: "PassiveEquipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PduHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PduId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PduHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PduHistoryEntries_Pdus_PduId",
                        column: x => x.PduId,
                        principalTable: "Pdus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhoneHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhoneId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhoneHistoryEntries_Phones_PhoneId",
                        column: x => x.PhoneId,
                        principalTable: "Phones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cartridges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CartridgeItemId = table.Column<int>(type: "int", nullable: false),
                    PrinterId = table.Column<int>(type: "int", nullable: true),
                    DateIn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUse = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateOut = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LevelPercent = table.Column<int>(type: "int", nullable: true),
                    LevelReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cartridges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cartridges_CartridgeItems_CartridgeItemId",
                        column: x => x.CartridgeItemId,
                        principalTable: "CartridgeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Cartridges_Printers_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PrinterHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrinterId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrinterHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrinterHistoryEntries_Printers_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RackHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RackId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RackHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RackHistoryEntries_Racks_RackId",
                        column: x => x.RackId,
                        principalTable: "Racks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SimCardHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SimCardId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimCardHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimCardHistoryEntries_SimCards_SimCardId",
                        column: x => x.SimCardId,
                        principalTable: "SimCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseArticleHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseArticleHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseArticleHistoryEntries_KnowledgeBaseArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KnowledgeBaseArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseArticleRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EditorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RevisedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseArticleRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseArticleRevisions_KnowledgeBaseArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KnowledgeBaseArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseArticleTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    ScopeEntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseArticleTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseArticleTargets_KnowledgeBaseArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KnowledgeBaseArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    GroupId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationRecipients_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NotificationRecipients_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationRecipients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "QueuedNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationId = table.Column<int>(type: "int", nullable: true),
                    NotificationName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecipientEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueuedNotifications_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ChangeValidations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeId = table.Column<int>(type: "int", nullable: false),
                    ValidatorUserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidationComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequesterName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeValidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeValidations_Changes_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Changes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeProblems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeId = table.Column<int>(type: "int", nullable: false),
                    ProblemId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeProblems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeProblems_Changes_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Changes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeProblems_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    PackageId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Log = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentJobs_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentJobs_DeploymentPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "DeploymentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentJobs_DeploymentTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "DeploymentTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentTaskPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentTaskId = table.Column<int>(type: "int", nullable: false),
                    PackageId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentTaskPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentTaskPackages_DeploymentPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "DeploymentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentTaskPackages_DeploymentTasks_DeploymentTaskId",
                        column: x => x.DeploymentTaskId,
                        principalTable: "DeploymentTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentTaskTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentTaskId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    ComputerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentTaskTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentTaskTargets_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentTaskTargets_DeployComputerGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "DeployComputerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentTaskTargets_DeploymentTasks_DeploymentTaskId",
                        column: x => x.DeploymentTaskId,
                        principalTable: "DeploymentTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscoveredNetworkDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MacAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hostname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SysDescr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SysName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SysContact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SysLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GuessedType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscoveredViaNetworkTaskId = table.Column<int>(type: "int", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PromotedNetworkEquipmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveredNetworkDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscoveredNetworkDevices_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscoveredNetworkDevices_NetworkEquipments_PromotedNetworkEquipmentId",
                        column: x => x.PromotedNetworkEquipmentId,
                        principalTable: "NetworkEquipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DiscoveredNetworkDevices_NetworkTasks_DiscoveredViaNetworkTaskId",
                        column: x => x.DiscoveredViaNetworkTaskId,
                        principalTable: "NetworkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTaskActors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkTaskId = table.Column<int>(type: "int", nullable: false),
                    AgentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTaskActors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTaskActors_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkTaskActors_NetworkTasks_NetworkTaskId",
                        column: x => x.NetworkTaskId,
                        principalTable: "NetworkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTaskCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkTaskId = table.Column<int>(type: "int", nullable: false),
                    SnmpCredentialId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTaskCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTaskCredentials_NetworkTasks_NetworkTaskId",
                        column: x => x.NetworkTaskId,
                        principalTable: "NetworkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkTaskCredentials_SnmpCredentials_SnmpCredentialId",
                        column: x => x.SnmpCredentialId,
                        principalTable: "SnmpCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTaskIpRanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkTaskId = table.Column<int>(type: "int", nullable: false),
                    IpRangeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTaskIpRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTaskIpRanges_IpRanges_IpRangeId",
                        column: x => x.IpRangeId,
                        principalTable: "IpRanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkTaskIpRanges_NetworkTasks_NetworkTaskId",
                        column: x => x.NetworkTaskId,
                        principalTable: "NetworkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTaskJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    NetworkTaskId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Log = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTaskJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTaskJobs_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkTaskJobs_NetworkTasks_NetworkTaskId",
                        column: x => x.NetworkTaskId,
                        principalTable: "NetworkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WakeOnLanTaskActors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WakeOnLanTaskId = table.Column<int>(type: "int", nullable: false),
                    AgentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WakeOnLanTaskActors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskActors_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskActors_WakeOnLanTasks_WakeOnLanTaskId",
                        column: x => x.WakeOnLanTaskId,
                        principalTable: "WakeOnLanTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WakeOnLanTaskJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    WakeOnLanTaskId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetMacsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Log = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WakeOnLanTaskJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskJobs_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskJobs_WakeOnLanTasks_WakeOnLanTaskId",
                        column: x => x.WakeOnLanTaskId,
                        principalTable: "WakeOnLanTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WakeOnLanTaskTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WakeOnLanTaskId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    ComputerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WakeOnLanTaskTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskTargets_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskTargets_DeployComputerGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "DeployComputerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskTargets_WakeOnLanTasks_WakeOnLanTaskId",
                        column: x => x.WakeOnLanTaskId,
                        principalTable: "WakeOnLanTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLevelEscalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceLevelAgreementId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OffsetMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLevelEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLevelEscalations_ServiceLevelAgreements_ServiceLevelAgreementId",
                        column: x => x.ServiceLevelAgreementId,
                        principalTable: "ServiceLevelAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsPriorityManual = table.Column<bool>(type: "bit", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    RequesterUserId = table.Column<int>(type: "int", nullable: true),
                    RequesterName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedUserId = table.Column<int>(type: "int", nullable: true),
                    AssignedGroupId = table.Column<int>(type: "int", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaTimeToOwnId = table.Column<int>(type: "int", nullable: true),
                    SlaTimeToResolveId = table.Column<int>(type: "int", nullable: true),
                    OlaTimeToOwnId = table.Column<int>(type: "int", nullable: true),
                    OlaTimeToResolveId = table.Column<int>(type: "int", nullable: true),
                    TimeToOwn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeToResolve = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InternalTimeToOwn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InternalTimeToResolve = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TakenIntoAccountAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OlaStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Solution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SolutionType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_ServiceLevelAgreements_OlaTimeToOwnId",
                        column: x => x.OlaTimeToOwnId,
                        principalTable: "ServiceLevelAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_ServiceLevelAgreements_OlaTimeToResolveId",
                        column: x => x.OlaTimeToResolveId,
                        principalTable: "ServiceLevelAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_ServiceLevelAgreements_SlaTimeToOwnId",
                        column: x => x.SlaTimeToOwnId,
                        principalTable: "ServiceLevelAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_ServiceLevelAgreements_SlaTimeToResolveId",
                        column: x => x.SlaTimeToResolveId,
                        principalTable: "ServiceLevelAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_TicketCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentPackageFileParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentPackageFileId = table.Column<int>(type: "int", nullable: false),
                    PartIndex = table.Column<int>(type: "int", nullable: false),
                    Sha512 = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentPackageFileParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentPackageFileParts_DeploymentPackageFiles_DeploymentPackageFileId",
                        column: x => x.DeploymentPackageFileId,
                        principalTable: "DeploymentPackageFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeripheralHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeripheralId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeripheralHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeripheralHistoryEntries_Peripherals_PeripheralId",
                        column: x => x.PeripheralId,
                        principalTable: "Peripherals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLevelEscalationActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceLevelEscalationId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLevelEscalationActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLevelEscalationActions_ServiceLevelEscalations_ServiceLevelEscalationId",
                        column: x => x.ServiceLevelEscalationId,
                        principalTable: "ServiceLevelEscalations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeId = table.Column<int>(type: "int", nullable: false),
                    TicketId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeTickets_Changes_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Changes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeTickets_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProblemTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemId = table.Column<int>(type: "int", nullable: false),
                    TicketId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProblemTickets_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProblemTickets_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketEscalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    ServiceLevelEscalationId = table.Column<int>(type: "int", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketEscalations_ServiceLevelEscalations_ServiceLevelEscalationId",
                        column: x => x.ServiceLevelEscalationId,
                        principalTable: "ServiceLevelEscalations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketEscalations_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agents_AgentUuid",
                table: "Agents",
                column: "AgentUuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agents_EntityId",
                table: "Agents",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Appliances_ContractId",
                table: "Appliances",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Appliances_EntityId",
                table: "Appliances",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Appliances_SupplierId",
                table: "Appliances",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistanceHistoryEntries_ItemType_ItemId_OccurredAt",
                table: "AssistanceHistoryEntries",
                columns: new[] { "ItemType", "ItemId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthLdapServers_Name",
                table: "AuthLdapServers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthMailServers_Name",
                table: "AuthMailServers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomaticActionRunLogs_TaskKey_RanAt",
                table: "AutomaticActionRunLogs",
                columns: new[] { "TaskKey", "RanAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomaticActionStates_TaskKey",
                table: "AutomaticActionStates",
                column: "TaskKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_EntityId",
                table: "Budgets",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CableHistoryEntries_CableId",
                table: "CableHistoryEntries",
                column: "CableId");

            migrationBuilder.CreateIndex(
                name: "IX_Cables_EntityId",
                table: "Cables",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Cables_StatusId",
                table: "Cables",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarHolidays_CalendarId",
                table: "CalendarHolidays",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_Calendars_EntityId",
                table: "Calendars",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSegments_CalendarId",
                table: "CalendarSegments",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_CartridgeItemHistoryEntries_CartridgeItemId",
                table: "CartridgeItemHistoryEntries",
                column: "CartridgeItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CartridgeItems_EntityId",
                table: "CartridgeItems",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CartridgeItems_LocationId",
                table: "CartridgeItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Cartridges_CartridgeItemId",
                table: "Cartridges",
                column: "CartridgeItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Cartridges_PrinterId",
                table: "Cartridges",
                column: "PrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_ContractId",
                table: "Certificates",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_EntityId",
                table: "Certificates",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_SupplierId",
                table: "Certificates",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeProblems_ChangeId_ProblemId",
                table: "ChangeProblems",
                columns: new[] { "ChangeId", "ProblemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeProblems_ProblemId",
                table: "ChangeProblems",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_Changes_AssignedUserId",
                table: "Changes",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Changes_CategoryId",
                table: "Changes",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Changes_EntityId",
                table: "Changes",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Changes_Status_OpenedAt",
                table: "Changes",
                columns: new[] { "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeTickets_ChangeId_TicketId",
                table: "ChangeTickets",
                columns: new[] { "ChangeId", "TicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeTickets_TicketId",
                table: "ChangeTickets",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeValidations_ChangeId",
                table: "ChangeValidations",
                column: "ChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeValidations_ValidatorUserId_Status",
                table: "ChangeValidations",
                columns: new[] { "ValidatorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Clusters_EntityId",
                table: "Clusters",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectDefinitions_EntityId",
                table: "CollectDefinitions",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectFileSearchEntries_CollectDefinitionId",
                table: "CollectFileSearchEntries",
                column: "CollectDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectRegistryEntries_CollectDefinitionId",
                table: "CollectRegistryEntries",
                column: "CollectDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectResults_CollectDefinitionId",
                table: "CollectResults",
                column: "CollectDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectResults_ComputerId_CollectDefinitionId_EntryName",
                table: "CollectResults",
                columns: new[] { "ComputerId", "CollectDefinitionId", "EntryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectWmiEntries_CollectDefinitionId",
                table: "CollectWmiEntries",
                column: "CollectDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerAntiviruses_ComputerId",
                table: "ComputerAntiviruses",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerBatteries_ComputerId",
                table: "ComputerBatteries",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerComponents_ComputerId",
                table: "ComputerComponents",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerConnectors_ComputerId",
                table: "ComputerConnectors",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerHistoryEntries_ComputerId",
                table: "ComputerHistoryEntries",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerImportHistories_ComputerId",
                table: "ComputerImportHistories",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerNetworkPorts_ComputerId",
                table: "ComputerNetworkPorts",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerPeripherals_ComputerId",
                table: "ComputerPeripherals",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerPeripherals_StatusId",
                table: "ComputerPeripherals",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerRuleAction_ComputerRuleId",
                table: "ComputerRuleAction",
                column: "ComputerRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerRuleCriterion_ComputerRuleId",
                table: "ComputerRuleCriterion",
                column: "ComputerRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerRules_EntityId",
                table: "ComputerRules",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Computers_AgentId",
                table: "Computers",
                column: "AgentId",
                unique: true,
                filter: "[AgentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Computers_EntityId",
                table: "Computers",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Computers_LocationId",
                table: "Computers",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Computers_SourceGlpiId",
                table: "Computers",
                column: "SourceGlpiId",
                unique: true,
                filter: "\"SourceGlpiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Computers_StatusId",
                table: "Computers",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerSoftwares_ComputerId",
                table: "ComputerSoftwares",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerVolumes_ComputerId",
                table: "ComputerVolumes",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableItemHistoryEntries_ConsumableItemId",
                table: "ConsumableItemHistoryEntries",
                column: "ConsumableItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableItems_EntityId",
                table: "ConsumableItems",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableItems_LocationId",
                table: "ConsumableItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Consumables_ConsumableItemId",
                table: "Consumables",
                column: "ConsumableItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_EntityId",
                table: "Contacts",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractCosts_BudgetId",
                table: "ContractCosts",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractCosts_ContractId",
                table: "ContractCosts",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_EntityId",
                table: "Contracts",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSuppliers_ContractId_SupplierId",
                table: "ContractSuppliers",
                columns: new[] { "ContractId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractSuppliers_SupplierId",
                table: "ContractSuppliers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssetDefinitions_SystemName",
                table: "CustomAssetDefinitions",
                column: "SystemName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssetFields_CustomAssetDefinitionId",
                table: "CustomAssetFields",
                column: "CustomAssetDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssetHistoryEntries_CustomAssetId_OccurredAt",
                table: "CustomAssetHistoryEntries",
                columns: new[] { "CustomAssetId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssets_CustomAssetDefinitionId",
                table: "CustomAssets",
                column: "CustomAssetDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssets_EntityId",
                table: "CustomAssets",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssetValues_CustomAssetFieldId",
                table: "CustomAssetValues",
                column: "CustomAssetFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssetValues_CustomAssetId_CustomAssetFieldId",
                table: "CustomAssetValues",
                columns: new[] { "CustomAssetId", "CustomAssetFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DashboardCardPreferences_UserId",
                table: "DashboardCardPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseInstances_EntityId",
                table: "DatabaseInstances",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Datacenters_ContractId",
                table: "Datacenters",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Datacenters_EntityId",
                table: "Datacenters",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Datacenters_SupplierId",
                table: "Datacenters",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_DeployComputerGroupCriteria_DeployComputerGroupId",
                table: "DeployComputerGroupCriteria",
                column: "DeployComputerGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DeployComputerGroupMembers_ComputerId",
                table: "DeployComputerGroupMembers",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeployComputerGroupMembers_DeployComputerGroupId_ComputerId",
                table: "DeployComputerGroupMembers",
                columns: new[] { "DeployComputerGroupId", "ComputerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeployComputerGroups_EntityId",
                table: "DeployComputerGroups",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentJobs_AgentId",
                table: "DeploymentJobs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentJobs_PackageId",
                table: "DeploymentJobs",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentJobs_TaskId",
                table: "DeploymentJobs",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentMirrorServers_EntityId",
                table: "DeploymentMirrorServers",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentMirrorServers_Name",
                table: "DeploymentMirrorServers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageFileParts_DeploymentPackageFileId",
                table: "DeploymentPackageFileParts",
                column: "DeploymentPackageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageFileParts_Sha512",
                table: "DeploymentPackageFileParts",
                column: "Sha512");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageFiles_DeploymentPackageId",
                table: "DeploymentPackageFiles",
                column: "DeploymentPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackages_DeployComputerGroupId",
                table: "DeploymentPackages",
                column: "DeployComputerGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackages_EntityId",
                table: "DeploymentPackages",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackages_SupersededByPackageId",
                table: "DeploymentPackages",
                column: "SupersededByPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageTargets_DeploymentPackageId",
                table: "DeploymentPackageTargets",
                column: "DeploymentPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRuleAction_DeploymentRuleId",
                table: "DeploymentRuleAction",
                column: "DeploymentRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRuleAction_PackageId",
                table: "DeploymentRuleAction",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRuleCriterion_DeploymentRuleId",
                table: "DeploymentRuleCriterion",
                column: "DeploymentRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRules_EntityId",
                table: "DeploymentRules",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTaskPackages_DeploymentTaskId_PackageId",
                table: "DeploymentTaskPackages",
                columns: new[] { "DeploymentTaskId", "PackageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTaskPackages_PackageId",
                table: "DeploymentTaskPackages",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTasks_EntityId",
                table: "DeploymentTasks",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTasks_ExecutionTimeSlotId",
                table: "DeploymentTasks",
                column: "ExecutionTimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTasks_PreparationTimeSlotId",
                table: "DeploymentTasks",
                column: "PreparationTimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTaskTargets_ComputerId",
                table: "DeploymentTaskTargets",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTaskTargets_DeploymentTaskId",
                table: "DeploymentTaskTargets",
                column: "DeploymentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTaskTargets_GroupId",
                table: "DeploymentTaskTargets",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentUserInteractionTemplates_EntityId",
                table: "DeploymentUserInteractionTemplates",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentUserInteractionTemplates_Name",
                table: "DeploymentUserInteractionTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DictionaryRuleCriteria_DictionaryRuleId",
                table: "DictionaryRuleCriteria",
                column: "DictionaryRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_DictionaryRules_EntityId",
                table: "DictionaryRules",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredNetworkDevices_DiscoveredViaNetworkTaskId",
                table: "DiscoveredNetworkDevices",
                column: "DiscoveredViaNetworkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredNetworkDevices_EntityId",
                table: "DiscoveredNetworkDevices",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredNetworkDevices_PromotedNetworkEquipmentId",
                table: "DiscoveredNetworkDevices",
                column: "PromotedNetworkEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCategories_EntityId",
                table: "DocumentCategories",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCategories_ParentId",
                table: "DocumentCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentItems_DocumentId_ItemType_ItemId",
                table: "DocumentItems",
                columns: new[] { "DocumentId", "ItemType", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentItems_ItemType_ItemId",
                table: "DocumentItems",
                columns: new[] { "ItemType", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CategoryId",
                table: "Documents",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_EntityId",
                table: "Documents",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Sha256",
                table: "Documents",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SourceGlpiId",
                table: "Documents",
                column: "SourceGlpiId");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_ContractId",
                table: "Domains",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_EntityId",
                table: "Domains",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_SupplierId",
                table: "Domains",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_DropdownItems_EntityId",
                table: "DropdownItems",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DropdownItems_ParentId",
                table: "DropdownItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DropdownItems_Type_EntityId_ParentId_Name",
                table: "DropdownItems",
                columns: new[] { "Type", "EntityId", "ParentId", "Name" },
                unique: true,
                filter: "[EntityId] IS NOT NULL AND [ParentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EnclosureHistoryEntries_EnclosureId",
                table: "EnclosureHistoryEntries",
                column: "EnclosureId");

            migrationBuilder.CreateIndex(
                name: "IX_Enclosures_EntityId",
                table: "Enclosures",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Enclosures_LocationId",
                table: "Enclosures",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Enclosures_StatusId",
                table: "Enclosures",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Entities_ParentId",
                table: "Entities",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Entities_SourceGlpiId",
                table: "Entities",
                column: "SourceGlpiId",
                unique: true,
                filter: "\"SourceGlpiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EntityHistoryEntries_EntityId",
                table: "EntityHistoryEntries",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_EntityNotes_EntityId",
                table: "EntityNotes",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLinkItemTypes_ExternalLinkId_ItemType",
                table: "ExternalLinkItemTypes",
                columns: new[] { "ExternalLinkId", "ItemType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLinkItemTypes_ItemType",
                table: "ExternalLinkItemTypes",
                column: "ItemType");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLinks_EntityId",
                table: "ExternalLinks",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldUnicityCriteria_EntityId",
                table: "FieldUnicityCriteria",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldUnicityCriteria_ItemType_IsActive",
                table: "FieldUnicityCriteria",
                columns: new[] { "ItemType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldUnicityFields_FieldUnicityCriterionId_FieldName",
                table: "FieldUnicityFields",
                columns: new[] { "FieldUnicityCriterionId", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupHistoryEntries_GroupId",
                table: "GroupHistoryEntries",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupNotes_GroupId",
                table: "GroupNotes",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_EntityId",
                table: "Groups",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_ParentId",
                table: "Groups",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_SourceGlpiId",
                table: "Groups",
                column: "SourceGlpiId",
                unique: true,
                filter: "\"SourceGlpiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GroupUsers_UserId",
                table: "GroupUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportAssignmentRuleAction_ImportAssignmentRuleId",
                table: "ImportAssignmentRuleAction",
                column: "ImportAssignmentRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportAssignmentRuleCriterion_ImportAssignmentRuleId",
                table: "ImportAssignmentRuleCriterion",
                column: "ImportAssignmentRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportAssignmentRules_EntityId",
                table: "ImportAssignmentRules",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBlacklistEntries_EntityId",
                table: "ImportBlacklistEntries",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_IpRanges_EntityId",
                table: "IpRanges",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ItilFollowups_ItemType_ItemId_CreatedAt",
                table: "ItilFollowups",
                columns: new[] { "ItemType", "ItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ItilTasks_ItemType_ItemId",
                table: "ItilTasks",
                columns: new[] { "ItemType", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticleHistoryEntries_ArticleId_OccurredAt",
                table: "KnowledgeBaseArticleHistoryEntries",
                columns: new[] { "ArticleId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticleRevisions_ArticleId_Number",
                table: "KnowledgeBaseArticleRevisions",
                columns: new[] { "ArticleId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticles_CategoryId",
                table: "KnowledgeBaseArticles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticles_EntityId",
                table: "KnowledgeBaseArticles",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticleTargets_ArticleId_Type_ItemId_ScopeEntityId",
                table: "KnowledgeBaseArticleTargets",
                columns: new[] { "ArticleId", "Type", "ItemId", "ScopeEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseCategories_EntityId",
                table: "KnowledgeBaseCategories",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseCategories_ParentId",
                table: "KnowledgeBaseCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_LockedFields_ItemType_ItemId_Field",
                table: "LockedFields",
                columns: new[] { "ItemType", "ItemId", "Field" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagementHistoryEntries_ItemType_ItemId_OccurredAt",
                table: "ManagementHistoryEntries",
                columns: new[] { "ItemType", "ItemId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEquipmentHistoryEntries_NetworkEquipmentId",
                table: "NetworkEquipmentHistoryEntries",
                column: "NetworkEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEquipments_EntityId",
                table: "NetworkEquipments",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEquipments_LocationId",
                table: "NetworkEquipments",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEquipments_StatusId",
                table: "NetworkEquipments",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskActors_AgentId",
                table: "NetworkTaskActors",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskActors_NetworkTaskId_AgentId",
                table: "NetworkTaskActors",
                columns: new[] { "NetworkTaskId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskCredentials_NetworkTaskId_SnmpCredentialId",
                table: "NetworkTaskCredentials",
                columns: new[] { "NetworkTaskId", "SnmpCredentialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskCredentials_SnmpCredentialId",
                table: "NetworkTaskCredentials",
                column: "SnmpCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskIpRanges_IpRangeId",
                table: "NetworkTaskIpRanges",
                column: "IpRangeId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskIpRanges_NetworkTaskId_IpRangeId",
                table: "NetworkTaskIpRanges",
                columns: new[] { "NetworkTaskId", "IpRangeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskJobs_AgentId",
                table: "NetworkTaskJobs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskJobs_NetworkTaskId",
                table: "NetworkTaskJobs",
                column: "NetworkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTasks_EntityId",
                table: "NetworkTasks",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTasks_ExecutionTimeSlotId",
                table: "NetworkTasks",
                column: "ExecutionTimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Notepads_ItemType_ItemId",
                table: "Notepads",
                columns: new[] { "ItemType", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_GroupId",
                table: "NotificationRecipients",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_NotificationId",
                table: "NotificationRecipients",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_UserId",
                table: "NotificationRecipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EntityId",
                table: "Notifications",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_NotificationTemplateId",
                table: "Notifications",
                column: "NotificationTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_EntityId",
                table: "NotificationTemplates",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthClients_ClientId",
                table: "OAuthClients",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OAuthClients_Name",
                table: "OAuthClients",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PassiveEquipmentHistoryEntries_PassiveEquipmentId",
                table: "PassiveEquipmentHistoryEntries",
                column: "PassiveEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PassiveEquipments_EntityId",
                table: "PassiveEquipments",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_PassiveEquipments_LocationId",
                table: "PassiveEquipments",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PassiveEquipments_StatusId",
                table: "PassiveEquipments",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PduHistoryEntries_PduId",
                table: "PduHistoryEntries",
                column: "PduId");

            migrationBuilder.CreateIndex(
                name: "IX_Pdus_EntityId",
                table: "Pdus",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Pdus_LocationId",
                table: "Pdus",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Pdus_StatusId",
                table: "Pdus",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PeripheralHistoryEntries_PeripheralId",
                table: "PeripheralHistoryEntries",
                column: "PeripheralId");

            migrationBuilder.CreateIndex(
                name: "IX_Peripherals_ComputerId",
                table: "Peripherals",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_Peripherals_EntityId",
                table: "Peripherals",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Peripherals_StatusId",
                table: "Peripherals",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneHistoryEntries_PhoneId",
                table: "PhoneHistoryEntries",
                column: "PhoneId");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneLines_ContractId",
                table: "PhoneLines",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneLines_EntityId",
                table: "PhoneLines",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneLines_SupplierId",
                table: "PhoneLines",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Phones_EntityId",
                table: "Phones",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Phones_LocationId",
                table: "Phones",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Phones_StatusId",
                table: "Phones",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PrinterHistoryEntries_PrinterId",
                table: "PrinterHistoryEntries",
                column: "PrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_EntityId",
                table: "Printers",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_LocationId",
                table: "Printers",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_StatusId",
                table: "Printers",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_AssignedUserId",
                table: "Problems",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_CategoryId",
                table: "Problems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_EntityId",
                table: "Problems",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_Status_OpenedAt",
                table: "Problems",
                columns: new[] { "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProblemTickets_ProblemId_TicketId",
                table: "ProblemTickets",
                columns: new[] { "ProblemId", "TicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemTickets_TicketId",
                table: "ProblemTickets",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileHistoryEntries_ProfileId",
                table: "ProfileHistoryEntries",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_SourceGlpiId",
                table: "Profiles",
                column: "SourceGlpiId",
                unique: true,
                filter: "\"SourceGlpiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QueuedNotifications_NotificationId",
                table: "QueuedNotifications",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_QueuedWebhooks_Status_CreatedAt",
                table: "QueuedWebhooks",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QueuedWebhooks_WebhookId",
                table: "QueuedWebhooks",
                column: "WebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_RackHistoryEntries_RackId",
                table: "RackHistoryEntries",
                column: "RackId");

            migrationBuilder.CreateIndex(
                name: "IX_Racks_EntityId",
                table: "Racks",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Racks_LocationId",
                table: "Racks",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Racks_StatusId",
                table: "Racks",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_EntityId",
                table: "SavedSearches",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearchOrders_UserId_SavedSearchId",
                table: "SavedSearchOrders",
                columns: new[] { "UserId", "SavedSearchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevelAgreements_ServiceLevelId",
                table: "ServiceLevelAgreements",
                column: "ServiceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevelEscalationActions_ServiceLevelEscalationId",
                table: "ServiceLevelEscalationActions",
                column: "ServiceLevelEscalationId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevelEscalations_ServiceLevelAgreementId",
                table: "ServiceLevelEscalations",
                column: "ServiceLevelAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevels_CalendarId",
                table: "ServiceLevels",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevels_EntityId",
                table: "ServiceLevels",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SimCardHistoryEntries_SimCardId",
                table: "SimCardHistoryEntries",
                column: "SimCardId");

            migrationBuilder.CreateIndex(
                name: "IX_SimCards_EntityId",
                table: "SimCards",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SimCards_LocationId",
                table: "SimCards",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SimCards_StatusId",
                table: "SimCards",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_SnmpCredentials_EntityId",
                table: "SnmpCredentials",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareLicenses_ContractId",
                table: "SoftwareLicenses",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareLicenses_EntityId",
                table: "SoftwareLicenses",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareLicenses_SupplierId",
                table: "SoftwareLicenses",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_ContactId",
                table: "SupplierContacts",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_SupplierId_ContactId",
                table: "SupplierContacts",
                columns: new[] { "SupplierId", "ContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_EntityId",
                table: "Suppliers",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_TableColumnPreferences_UserId_ItemType",
                table: "TableColumnPreferences",
                columns: new[] { "UserId", "ItemType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_EntityId",
                table: "TicketCategories",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_ParentId",
                table: "TicketCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalations_ServiceLevelEscalationId",
                table: "TicketEscalations",
                column: "ServiceLevelEscalationId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalations_TicketId_ServiceLevelEscalationId",
                table: "TicketEscalations",
                columns: new[] { "TicketId", "ServiceLevelEscalationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedUserId",
                table: "Tickets",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CategoryId",
                table: "Tickets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_EntityId",
                table: "Tickets",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OlaTimeToOwnId",
                table: "Tickets",
                column: "OlaTimeToOwnId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OlaTimeToResolveId",
                table: "Tickets",
                column: "OlaTimeToResolveId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SlaTimeToOwnId",
                table: "Tickets",
                column: "SlaTimeToOwnId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SlaTimeToResolveId",
                table: "Tickets",
                column: "SlaTimeToResolveId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_OpenedAt",
                table: "Tickets",
                columns: new[] { "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TimeToResolve",
                table: "Tickets",
                column: "TimeToResolve");

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlotEntries_TimeSlotId",
                table: "TimeSlotEntries",
                column: "TimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlots_EntityId",
                table: "TimeSlots",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserHistoryEntries_UserId",
                table: "UserHistoryEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_EntityId",
                table: "UserProfiles",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_ProfileId",
                table: "UserProfiles",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ApiTokenHash",
                table: "Users",
                column: "ApiTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_Users_LdapServerId",
                table: "Users",
                column: "LdapServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SourceGlpiId",
                table: "Users",
                column: "SourceGlpiId",
                unique: true,
                filter: "\"SourceGlpiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskActors_AgentId",
                table: "WakeOnLanTaskActors",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskActors_WakeOnLanTaskId_AgentId",
                table: "WakeOnLanTaskActors",
                columns: new[] { "WakeOnLanTaskId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskJobs_AgentId",
                table: "WakeOnLanTaskJobs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskJobs_WakeOnLanTaskId",
                table: "WakeOnLanTaskJobs",
                column: "WakeOnLanTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTasks_EntityId",
                table: "WakeOnLanTasks",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTasks_ExecutionTimeSlotId",
                table: "WakeOnLanTasks",
                column: "ExecutionTimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskTargets_ComputerId",
                table: "WakeOnLanTaskTargets",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskTargets_GroupId",
                table: "WakeOnLanTaskTargets",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskTargets_WakeOnLanTaskId",
                table: "WakeOnLanTaskTargets",
                column: "WakeOnLanTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookHeaders_WebhookId",
                table: "WebhookHeaders",
                column: "WebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_Webhooks_EntityId",
                table: "Webhooks",
                column: "EntityId");
            // ----------------------------------------------------------------------------------
            // Données d'installation. Une migration régénérée ne contient que le schéma : ces
            // lignes étaient portées par SeedRootEntity, SeedItCoordinatorGroup,
            // SeedSuperAdminProfile, SeedDefaultProfiles et SeedDefaultApiClient, toutes fondues
            // ici. Sans elles, une installation neuve n'a ni entité racine, ni profil, ni client
            // d'API — et l'API répondrait ERROR_NOT_ALLOWED_IP à tout appel, même local.
            //
            // Les profils sont insérés dans leur état final : Super-Admin n'est pas le profil par
            // défaut, Technician l'est, comme dans GLPI. Inutile de rejouer l'insertion puis la
            // correction que l'historique avait dû faire en deux temps.
            // ----------------------------------------------------------------------------------

            migrationBuilder.InsertData(
                table: "Entities",
                columns: new[] { "Name", "Comment", "CreatedAt", "UpdatedAt" },
                values: new object[]
                {
                    "Root entity", null, Seeded, Seeded
                });

            migrationBuilder.InsertData(
                table: "Groups",
                columns: new[]
                {
                    "Name", "Comment", "Code", "IsRecursive", "VisibleAsRequester", "VisibleAsObserver",
                    "VisibleAsAssignee", "VisibleAsTask", "CanBeNotified", "CanBeProjectSupervisor",
                    "CanContainItems", "CanContainUsers", "CreatedAt", "UpdatedAt"
                },
                values: new object[]
                {
                    "IT Coordinator", "Groupe contenant tous les IT Coordinator.", null, false, true, true,
                    true, true, true, true,
                    false, true, Seeded, Seeded
                });

            migrationBuilder.InsertData(
                table: "Profiles",
                columns: new[]
                {
                    "Name", "Comment", "IsDefault", "ParcRight", "AssistanceRight", "GestionRight",
                    "OutilsRight", "AdministrationRight", "ConfigurationRight", "CreatedAt", "UpdatedAt"
                },
                values: new object[,]
                {
                    {
                        "Super-Admin", "Accès complet à toutes les sections (droits non encore appliqués).", false,
                        2, 2, 2, 2, 2, 2, Seeded, Seeded
                    },
                    {
                        "Self-Service", "Interface simplifiée : accès en lecture à l'assistance uniquement (droits non encore appliqués).", false,
                        0, 1, 0, 0, 0, 0, Seeded, Seeded
                    },
                    {
                        "Admin", "Accès large à toutes les sections, hors configuration avancée (droits non encore appliqués).", false,
                        2, 2, 2, 2, 2, 1, Seeded, Seeded
                    },
                    {
                        "Technician", "Gère le parc et l'assistance au quotidien (droits non encore appliqués).", true,
                        2, 2, 1, 1, 0, 0, Seeded, Seeded
                    },
                });

            // Clients d'API de GLPI. Les noms restent les siens, en anglais : ce sont ses
            // enregistrements, et un client écrit pour GLPI peut les chercher.
            migrationBuilder.InsertData(
                table: "ApiClients",
                columns: new[]
                {
                    "Name", "IsActive", "Ipv4RangeStart", "Ipv4RangeEnd", "Ipv6",
                    "AppTokenHash", "AppTokenDate", "LogMethod", "Comment", "CreatedAt", "UpdatedAt"
                },
                values: new object[,]
                {
                    {
                        "full access from localhost", true, Localhost, Localhost, "::1",
                        null, null, 1, null, Seeded, Seeded
                    },
                    {
                        // Désactivé, comme chez GLPI : ouvrir l'API à toutes les adresses est une
                        // décision d'administrateur, pas un défaut d'installation.
                        "full access from anywhere", false, null, null, null,
                        null, null, 1, null, Seeded, Seeded
                    },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiClients");

            migrationBuilder.DropTable(
                name: "Appliances");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AssistanceHistoryEntries");

            migrationBuilder.DropTable(
                name: "AuthMailServers");

            migrationBuilder.DropTable(
                name: "AutomaticActionRunLogs");

            migrationBuilder.DropTable(
                name: "AutomaticActionStates");

            migrationBuilder.DropTable(
                name: "CableHistoryEntries");

            migrationBuilder.DropTable(
                name: "CalendarHolidays");

            migrationBuilder.DropTable(
                name: "CalendarSegments");

            migrationBuilder.DropTable(
                name: "CartridgeItemHistoryEntries");

            migrationBuilder.DropTable(
                name: "Cartridges");

            migrationBuilder.DropTable(
                name: "Certificates");

            migrationBuilder.DropTable(
                name: "ChangeProblems");

            migrationBuilder.DropTable(
                name: "ChangeTickets");

            migrationBuilder.DropTable(
                name: "ChangeValidations");

            migrationBuilder.DropTable(
                name: "Clusters");

            migrationBuilder.DropTable(
                name: "CollectFileSearchEntries");

            migrationBuilder.DropTable(
                name: "CollectRegistryEntries");

            migrationBuilder.DropTable(
                name: "CollectResults");

            migrationBuilder.DropTable(
                name: "CollectWmiEntries");

            migrationBuilder.DropTable(
                name: "ComputerAntiviruses");

            migrationBuilder.DropTable(
                name: "ComputerBatteries");

            migrationBuilder.DropTable(
                name: "ComputerComponents");

            migrationBuilder.DropTable(
                name: "ComputerConnectors");

            migrationBuilder.DropTable(
                name: "ComputerHistoryEntries");

            migrationBuilder.DropTable(
                name: "ComputerImportHistories");

            migrationBuilder.DropTable(
                name: "ComputerNetworkPorts");

            migrationBuilder.DropTable(
                name: "ComputerPeripherals");

            migrationBuilder.DropTable(
                name: "ComputerRuleAction");

            migrationBuilder.DropTable(
                name: "ComputerRuleCriterion");

            migrationBuilder.DropTable(
                name: "ComputerSoftwares");

            migrationBuilder.DropTable(
                name: "ComputerVolumes");

            migrationBuilder.DropTable(
                name: "ConsumableItemHistoryEntries");

            migrationBuilder.DropTable(
                name: "Consumables");

            migrationBuilder.DropTable(
                name: "ContractCosts");

            migrationBuilder.DropTable(
                name: "ContractSuppliers");

            migrationBuilder.DropTable(
                name: "CronSettings");

            migrationBuilder.DropTable(
                name: "CustomAssetHistoryEntries");

            migrationBuilder.DropTable(
                name: "CustomAssetValues");

            migrationBuilder.DropTable(
                name: "DashboardCardPreferences");

            migrationBuilder.DropTable(
                name: "DatabaseInstances");

            migrationBuilder.DropTable(
                name: "Datacenters");

            migrationBuilder.DropTable(
                name: "DeployComputerGroupCriteria");

            migrationBuilder.DropTable(
                name: "DeployComputerGroupMembers");

            migrationBuilder.DropTable(
                name: "DeploymentJobs");

            migrationBuilder.DropTable(
                name: "DeploymentMirrorServers");

            migrationBuilder.DropTable(
                name: "DeploymentPackageFileParts");

            migrationBuilder.DropTable(
                name: "DeploymentPackageTargets");

            migrationBuilder.DropTable(
                name: "DeploymentRuleAction");

            migrationBuilder.DropTable(
                name: "DeploymentRuleCriterion");

            migrationBuilder.DropTable(
                name: "DeploymentTaskPackages");

            migrationBuilder.DropTable(
                name: "DeploymentTaskTargets");

            migrationBuilder.DropTable(
                name: "DeploymentUserInteractionTemplates");

            migrationBuilder.DropTable(
                name: "DictionaryRuleCriteria");

            migrationBuilder.DropTable(
                name: "DiscoveredNetworkDevices");

            migrationBuilder.DropTable(
                name: "DocumentItems");

            migrationBuilder.DropTable(
                name: "Domains");

            migrationBuilder.DropTable(
                name: "EnclosureHistoryEntries");

            migrationBuilder.DropTable(
                name: "EntityHistoryEntries");

            migrationBuilder.DropTable(
                name: "EntityNotes");

            migrationBuilder.DropTable(
                name: "EventLogEntries");

            migrationBuilder.DropTable(
                name: "ExternalLinkItemTypes");

            migrationBuilder.DropTable(
                name: "FieldUnicityFields");

            migrationBuilder.DropTable(
                name: "GroupHistoryEntries");

            migrationBuilder.DropTable(
                name: "GroupNotes");

            migrationBuilder.DropTable(
                name: "GroupUsers");

            migrationBuilder.DropTable(
                name: "ImportAssignmentRuleAction");

            migrationBuilder.DropTable(
                name: "ImportAssignmentRuleCriterion");

            migrationBuilder.DropTable(
                name: "ImportBlacklistEntries");

            migrationBuilder.DropTable(
                name: "ItilFollowups");

            migrationBuilder.DropTable(
                name: "ItilTasks");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseArticleHistoryEntries");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseArticleRevisions");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseArticleTargets");

            migrationBuilder.DropTable(
                name: "LockedFields");

            migrationBuilder.DropTable(
                name: "ManagementHistoryEntries");

            migrationBuilder.DropTable(
                name: "NetworkEquipmentHistoryEntries");

            migrationBuilder.DropTable(
                name: "NetworkTaskActors");

            migrationBuilder.DropTable(
                name: "NetworkTaskCredentials");

            migrationBuilder.DropTable(
                name: "NetworkTaskIpRanges");

            migrationBuilder.DropTable(
                name: "NetworkTaskJobs");

            migrationBuilder.DropTable(
                name: "Notepads");

            migrationBuilder.DropTable(
                name: "NotificationRecipients");

            migrationBuilder.DropTable(
                name: "OAuthClients");

            migrationBuilder.DropTable(
                name: "PassiveEquipmentHistoryEntries");

            migrationBuilder.DropTable(
                name: "PduHistoryEntries");

            migrationBuilder.DropTable(
                name: "PeripheralHistoryEntries");

            migrationBuilder.DropTable(
                name: "PhoneHistoryEntries");

            migrationBuilder.DropTable(
                name: "PhoneLines");

            migrationBuilder.DropTable(
                name: "PrinterHistoryEntries");

            migrationBuilder.DropTable(
                name: "ProblemTickets");

            migrationBuilder.DropTable(
                name: "ProfileHistoryEntries");

            migrationBuilder.DropTable(
                name: "QueuedNotifications");

            migrationBuilder.DropTable(
                name: "QueuedWebhooks");

            migrationBuilder.DropTable(
                name: "RackHistoryEntries");

            migrationBuilder.DropTable(
                name: "RefusedImportLogs");

            migrationBuilder.DropTable(
                name: "SavedSearches");

            migrationBuilder.DropTable(
                name: "SavedSearchOrders");

            migrationBuilder.DropTable(
                name: "ServiceLevelEscalationActions");

            migrationBuilder.DropTable(
                name: "SimCardHistoryEntries");

            migrationBuilder.DropTable(
                name: "SoftwareLicenses");

            migrationBuilder.DropTable(
                name: "SupplierContacts");

            migrationBuilder.DropTable(
                name: "TableColumnPreferences");

            migrationBuilder.DropTable(
                name: "TicketEscalations");

            migrationBuilder.DropTable(
                name: "TimeSlotEntries");

            migrationBuilder.DropTable(
                name: "UserHistoryEntries");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "WakeOnLanTaskActors");

            migrationBuilder.DropTable(
                name: "WakeOnLanTaskJobs");

            migrationBuilder.DropTable(
                name: "WakeOnLanTaskTargets");

            migrationBuilder.DropTable(
                name: "WebhookHeaders");

            migrationBuilder.DropTable(
                name: "Cables");

            migrationBuilder.DropTable(
                name: "CartridgeItems");

            migrationBuilder.DropTable(
                name: "Changes");

            migrationBuilder.DropTable(
                name: "CollectDefinitions");

            migrationBuilder.DropTable(
                name: "ComputerRules");

            migrationBuilder.DropTable(
                name: "ConsumableItems");

            migrationBuilder.DropTable(
                name: "Budgets");

            migrationBuilder.DropTable(
                name: "CustomAssetFields");

            migrationBuilder.DropTable(
                name: "CustomAssets");

            migrationBuilder.DropTable(
                name: "DeploymentPackageFiles");

            migrationBuilder.DropTable(
                name: "DeploymentRules");

            migrationBuilder.DropTable(
                name: "DeploymentTasks");

            migrationBuilder.DropTable(
                name: "DictionaryRules");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Enclosures");

            migrationBuilder.DropTable(
                name: "ExternalLinks");

            migrationBuilder.DropTable(
                name: "FieldUnicityCriteria");

            migrationBuilder.DropTable(
                name: "ImportAssignmentRules");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseArticles");

            migrationBuilder.DropTable(
                name: "NetworkEquipments");

            migrationBuilder.DropTable(
                name: "SnmpCredentials");

            migrationBuilder.DropTable(
                name: "IpRanges");

            migrationBuilder.DropTable(
                name: "NetworkTasks");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropTable(
                name: "PassiveEquipments");

            migrationBuilder.DropTable(
                name: "Pdus");

            migrationBuilder.DropTable(
                name: "Peripherals");

            migrationBuilder.DropTable(
                name: "Phones");

            migrationBuilder.DropTable(
                name: "Printers");

            migrationBuilder.DropTable(
                name: "Problems");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Racks");

            migrationBuilder.DropTable(
                name: "SimCards");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "ServiceLevelEscalations");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WakeOnLanTasks");

            migrationBuilder.DropTable(
                name: "Webhooks");

            migrationBuilder.DropTable(
                name: "CustomAssetDefinitions");

            migrationBuilder.DropTable(
                name: "DeploymentPackages");

            migrationBuilder.DropTable(
                name: "DocumentCategories");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseCategories");

            migrationBuilder.DropTable(
                name: "Computers");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "ServiceLevelAgreements");

            migrationBuilder.DropTable(
                name: "TicketCategories");

            migrationBuilder.DropTable(
                name: "AuthLdapServers");

            migrationBuilder.DropTable(
                name: "TimeSlots");

            migrationBuilder.DropTable(
                name: "DeployComputerGroups");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "DropdownItems");

            migrationBuilder.DropTable(
                name: "ServiceLevels");

            migrationBuilder.DropTable(
                name: "Calendars");

            migrationBuilder.DropTable(
                name: "Entities");
        }
    }
}
