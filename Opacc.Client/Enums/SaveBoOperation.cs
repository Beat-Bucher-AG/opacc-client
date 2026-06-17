namespace Opacc.Client.Enums;

public enum SaveBoOperation
{
    /// <summary>Update an existing record only. Fails if the key does not exist. (SaveBoProcessingCd=1)</summary>
    Update = 1,

    /// <summary>Create a new record only. Fails if the key already exists. (SaveBoProcessingCd=2)</summary>
    Create = 2,

    /// <summary>Update if the key exists, create otherwise. (SaveBoProcessingCd=3)</summary>
    CreateOrUpdate = 3,
}
