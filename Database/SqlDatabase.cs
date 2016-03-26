﻿using System;
using System.Data.SqlClient;
using DevSpace.Common;

namespace DevSpace.Database {
	public class SqlDatabase : IDatabase {
		private SqlConnection Connection;

		private bool ConnectToMaster() {
			try {
				SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder( Settings.ConnectionString );
				builder.InitialCatalog = "master";
				builder.ConnectTimeout = 300; // We have to give enough time for the database to be created

				Connection = new SqlConnection( builder.ConnectionString );
				Connection.Open();
				return true;
			} catch( Exception Ex ) {
				return false;
			}
		}

		private bool CreateVersionInfo() {
			SqlCommand Command = new SqlCommand();
			Command.Connection = Connection;
			Command.CommandText =
@"CREATE TABLE VersionInfo (
DbVersion	VARCHAR(16)	NOT NULL,

CONSTRAINT VersionInfo_PK PRIMARY KEY ( DbVersion )
);

INSERT VersionInfo ( DbVersion ) VALUES ( '00.00.00.0000' );";
			Command.ExecuteNonQuery();
			return true;
		}

		private bool ConnectToDb() {
			Connection = new SqlConnection( Settings.ConnectionString );
			Connection.Open();
			return true;
		}

		private bool DbVersionTableExists() {
			SqlCommand Command = new SqlCommand();
			Command.Connection = Connection;
			Command.CommandText =
@"IF(
	EXISTS(
		SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND  TABLE_NAME = 'VersionInfo'
	)
)
	SELECT CAST( 1 AS BIT );
ELSE
	SELECT CAST( 0 AS BIT );";
			return (bool)(Command.ExecuteScalar() ?? false);
		}

		private string GetDatabaseVersion() {
			if( DbVersionTableExists() ) {
				SqlCommand Command = new SqlCommand();
				Command.Connection = Connection;
				Command.CommandText = "SELECT DbVersion FROM VersionInfo;";
				return Command.ExecuteScalar()?.ToString();
			} else {
				if( CreateVersionInfo() ) {
					return "00.00.00.0000";
				} else {
					throw new Exception( "Could not create VersionInfo table" );
				}
			}
		}

		private string GetUpgradeScript( string DatabaseVersion ) {
			switch( DatabaseVersion ) {
				case "": // Because string.Empty is not a constant...
				case "00.00.00.0000":
					return
@"CREATE TABLE SponsorLevels (
	Id					INT			IDENTITY(1,1)	NOT NULL,
	DisplayOrder		INT							NOT NULL,
	DisplayName			VARCHAR(16)					NOT NULL,
	DisplayInEmails		BIT							NOT NULL,
	DisplayInSidebar	BIT							NOT NULL,

	CONSTRAINT SponsorLevels_PK PRIMARY KEY NONCLUSTERED ( Id ),
	CONSTRAINT SponsorLevels_CI UNIQUE CLUSTERED ( DisplayOrder )
);

CREATE TABLE Sponsors (
	Id				INT				IDENTITY(1,1)	NOT NULL,
	DisplayName		VARCHAR(16)						NOT NULL,
	Level			INT								NOT NULL,
	LogoSmall		VARCHAR(64)						NOT NULL,
	LogoLarge		VARCHAR(64)						NOT NULL,

	CONSTRAINT Sponsor_PK PRIMARY KEY ( Id ),
	CONSTRAINT Sponsors_SponsorLevels_FK FOREIGN KEY ( Level ) REFERENCES SponsorLevels ( Id )
);

UPDATE VersionInfo SET DbVersion = '01.00.00.0000';";

				default:
					return string.Empty;
			}
		}

		private bool RunUpgradeScript( string UpgradeScript ) {
			SqlCommand Command = new SqlCommand();
			Command.Connection = Connection;
			Command.CommandText = UpgradeScript;
			Command.ExecuteNonQuery();
			return true;
		}

		public void Initialize() {
			ConnectToDb();

			do {
				string UpgradeScript = GetUpgradeScript( GetDatabaseVersion() );

				if( string.IsNullOrWhiteSpace( UpgradeScript ) ) break;
				else RunUpgradeScript( UpgradeScript );
			} while( true );

			Connection.Close();
		}
	}
}