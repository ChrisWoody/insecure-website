drop table if exists [User]
go

create table [User]
(
	[Username] varchar(20) not null primary key,
	[Password] varchar(20) not null,
	[DateOfBirth] date not null,
	[HealthIdentifier] varchar(13) not null
)
go

insert into [User] ([Username], [Password], [DateOfBirth], [HealthIdentifier])
values ('Admin', 'Spohxmouo4oCK5srHvL', '1970-01-02', 'HIN4569871230') --SomethingVerySecret


drop table if exists [UserMessage]
go

create table [UserMessage]
(
	[Id] int identity(1,1) primary key,
	[Username] varchar(20) not null,
	[DisplayRaw] bit not null,
	[Hide] bit not null,
	[Message] nvarchar(2048) not null
)
go


drop table if exists [UserToUserMessage]
go

create table [UserToUserMessage]
(
	[Id] int identity(1,1) primary key,
	[FromUsername] varchar(20) not null,
	[ToUsername] varchar(20) not null,
	[Message] nvarchar(2048) not null
)
go
