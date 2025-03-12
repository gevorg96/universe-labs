using FluentMigrator.Runner.VersionTableInfo;

namespace UniverseLabs.Oms.Migrations;

public class VersionTable : IVersionTableMetaData
{
    public bool OwnsSchema => true;

    public string SchemaName => "public";

    public string TableName => "version_info";

    public string ColumnName => "version";

    public string DescriptionColumnName => "description";

    public string AppliedOnColumnName => "applied_on";

    public bool CreateWithPrimaryKey { get; } = false;

    public string UniqueIndexName => "uc_version";
}
