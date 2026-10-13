IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    CREATE TABLE [Clients] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [ContactPerson] nvarchar(100) NOT NULL,
        [ContactEmail] nvarchar(200) NOT NULL,
        [ContactPhone] nvarchar(30) NOT NULL,
        [Region] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Clients] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    CREATE TABLE [Contracts] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [ClientId] int NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ServiceLevel] nvarchar(20) NOT NULL,
        [SlaResponseHours] int NOT NULL,
        [SignedAgreementStoredName] nvarchar(64) NULL,
        [SignedAgreementOriginalName] nvarchar(255) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Contracts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Contracts_EndAfterStart] CHECK ([EndDate] > [StartDate]),
        CONSTRAINT [FK_Contracts_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    CREATE TABLE [ContractStatusHistory] (
        [Id] int NOT NULL IDENTITY,
        [ContractId] int NOT NULL,
        [FromStatus] nvarchar(20) NULL,
        [ToStatus] nvarchar(20) NOT NULL,
        [Reason] nvarchar(200) NOT NULL,
        [ChangedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ContractStatusHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractStatusHistory_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    CREATE TABLE [ServiceRequests] (
        [Id] int NOT NULL IDENTITY,
        [ContractId] int NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [CostUsd] decimal(18,2) NOT NULL,
        [CostZar] decimal(18,2) NOT NULL,
        [ExchangeRate] decimal(18,6) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ServiceRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ServiceRequests_CostUsd] CHECK ([CostUsd] > 0),
        CONSTRAINT [CK_ServiceRequests_ExchangeRate] CHECK ([ExchangeRate] > 0),
        CONSTRAINT [FK_ServiceRequests_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ContactEmail', N'ContactPerson', N'ContactPhone', N'Name', N'Region') AND [object_id] = OBJECT_ID(N'[Clients]'))
        SET IDENTITY_INSERT [Clients] ON;
    EXEC(N'INSERT INTO [Clients] ([Id], [ContactEmail], [ContactPerson], [ContactPhone], [Name], [Region])
    VALUES (1, N''thandi.nkosi@capefreight.example'', N''Thandi Nkosi'', N''+27 21 555 0101'', N''Cape Freight Holdings'', N''Africa''),
    (2, N''p.devries@rotterdam-maritime.example'', N''Pieter de Vries'', N''+31 10 555 0202'', N''Rotterdam Maritime BV'', N''Europe''),
    (3, N''mei.lin@pacificrim.example'', N''Mei Lin'', N''+65 6555 0303'', N''Pacific Rim Logistics'', N''Asia-Pacific'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ContactEmail', N'ContactPerson', N'ContactPhone', N'Name', N'Region') AND [object_id] = OBJECT_ID(N'[Clients]'))
        SET IDENTITY_INSERT [Clients] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClientId', N'CreatedAtUtc', N'EndDate', N'ServiceLevel', N'SignedAgreementOriginalName', N'SignedAgreementStoredName', N'SlaResponseHours', N'StartDate', N'Status', N'Title') AND [object_id] = OBJECT_ID(N'[Contracts]'))
        SET IDENTITY_INSERT [Contracts] ON;
    EXEC(N'INSERT INTO [Contracts] ([Id], [ClientId], [CreatedAtUtc], [EndDate], [ServiceLevel], [SignedAgreementOriginalName], [SignedAgreementStoredName], [SlaResponseHours], [StartDate], [Status], [Title])
    VALUES (1, 1, ''2026-01-15T08:00:00.0000000Z'', ''2027-12-31'', N''Premium'', NULL, NULL, 4, ''2026-01-01'', N''Active'', N''Durban port container handling''),
    (2, 2, ''2026-01-15T08:00:00.0000000Z'', ''2027-05-31'', N''Express'', NULL, NULL, 24, ''2025-06-01'', N''OnHold'', N''EU cross-dock distribution''),
    (3, 3, ''2026-01-15T08:00:00.0000000Z'', ''2025-12-31'', N''Standard'', NULL, NULL, 72, ''2025-01-01'', N''Expired'', N''Singapore air freight 2025''),
    (4, 1, ''2026-01-15T08:00:00.0000000Z'', ''2028-12-31'', N''Standard'', NULL, NULL, 72, ''2027-01-01'', N''Draft'', N''Johannesburg warehousing renewal'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClientId', N'CreatedAtUtc', N'EndDate', N'ServiceLevel', N'SignedAgreementOriginalName', N'SignedAgreementStoredName', N'SlaResponseHours', N'StartDate', N'Status', N'Title') AND [object_id] = OBJECT_ID(N'[Contracts]'))
        SET IDENTITY_INSERT [Contracts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ContractId', N'CostUsd', N'CostZar', N'CreatedAtUtc', N'Description', N'ExchangeRate', N'Status') AND [object_id] = OBJECT_ID(N'[ServiceRequests]'))
        SET IDENTITY_INSERT [ServiceRequests] ON;
    EXEC(N'INSERT INTO [ServiceRequests] ([Id], [ContractId], [CostUsd], [CostZar], [CreatedAtUtc], [Description], [ExchangeRate], [Status])
    VALUES (1, 1, 1200.0, 21900.0, ''2026-01-15T08:00:00.0000000Z'', N''Additional reefer container inspection'', 18.25, N''Approved'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ContractId', N'CostUsd', N'CostZar', N'CreatedAtUtc', N'Description', N'ExchangeRate', N'Status') AND [object_id] = OBJECT_ID(N'[ServiceRequests]'))
        SET IDENTITY_INSERT [ServiceRequests] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Clients_Name] ON [Clients] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contracts_ClientId] ON [Contracts] ([ClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contracts_StartDate] ON [Contracts] ([StartDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Contracts_Status_EndDate] ON [Contracts] ([Status], [EndDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContractStatusHistory_ContractId] ON [ContractStatusHistory] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ServiceRequests_ContractId] ON [ServiceRequests] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013144716_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013144716_InitialCreate', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013150758_AddUsers'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Username] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013150758_AddUsers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013150758_AddUsers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013150758_AddUsers', N'8.0.31');
END;
GO

COMMIT;
GO

