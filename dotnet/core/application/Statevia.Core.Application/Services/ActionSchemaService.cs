using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Publication;
using PublicationActionDescriptor = Statevia.Core.Actions.Abstractions.Publication.ActionDescriptor;

namespace Statevia.Core.Application.Services;

/// <summary><see cref="IActionCatalog"/> から Action Schema API 応答を組み立てる。</summary>
/// <remarks>
/// 退役した Action（<see cref="ActionCatalogEntry.IsRetired"/>）は一覧に出さず、個別取得は未登録と同じ 404 にする。
/// </remarks>
internal sealed class ActionSchemaService : IActionSchemaService
{
    private readonly IActionCatalog _catalog;

    /// <summary>
    /// <see cref="ActionSchemaService"/> を生成する。
    /// </summary>
    /// <param name="catalog">Action Catalog。</param>
    public ActionSchemaService(IActionCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public ActionSchemaListResponse GetList()
    {
        var items = GetListedActionIds()
            .Select(BuildListItem)
            .ToList();
        return new ActionSchemaListResponse { Items = items };
    }

    /// <inheritdoc />
    public ActionSchemaIndexResponse GetIndex()
    {
        var items = GetListedActionIds()
            .Select(BuildIndexItem)
            .Where(static item => item is not null)
            .Cast<ActionSchemaIndexItemDto>()
            .ToList();
        return new ActionSchemaIndexResponse { Items = items };
    }

    /// <inheritdoc />
    public ActionSchemaDetailResponse GetDetail(string actionId)
    {
        if (!_catalog.Exists(actionId) || IsRetired(actionId))
        {
            throw new NotFoundException($"Action '{actionId}' was not found.");
        }

        if (!_catalog.TryGetPublication(actionId, out var publication) || publication is null)
        {
            throw new NotFoundException($"Action '{actionId}' was not found.");
        }

        return MapDetail(publication);
    }

    /// <summary>一覧に出す actionId（退役した Action を除く）。</summary>
    private IEnumerable<string> GetListedActionIds() =>
        _catalog.GetRegisteredActionIds().Where(actionId => !IsRetired(actionId));

    /// <summary>退役した Action か。版が複数ロードされた Module の Action は退役させないので false。</summary>
    /// <param name="actionId">参照 actionId。</param>
    private bool IsRetired(string actionId) =>
        _catalog.TryGetRegistration(actionId, out var registration) && registration.Entry.IsRetired;

    private ActionSchemaListItemDto BuildListItem(string canonicalActionId)
    {
        _catalog.TryGetPublication(canonicalActionId, out var publication);
        if (publication is not null)
        {
            return new ActionSchemaListItemDto
            {
                ActionId = publication.Descriptor.ActionId,
                DisplayName = publication.Descriptor.DisplayName,
                Version = publication.Descriptor.Version,
                Category = publication.Descriptor.Category,
                HasSchema = true,
            };
        }

        _catalog.TryGetDescriptor(canonicalActionId, out var catalogDescriptor);
        return new ActionSchemaListItemDto
        {
            ActionId = canonicalActionId,
            DisplayName = canonicalActionId,
            Version = catalogDescriptor?.Version ?? "",
            HasSchema = false,
        };
    }

    private ActionSchemaIndexItemDto? BuildIndexItem(string canonicalActionId)
    {
        if (!_catalog.TryGetPublication(canonicalActionId, out var publication) || publication is null)
        {
            return null;
        }

        return new ActionSchemaIndexItemDto
        {
            ActionId = publication.Descriptor.ActionId,
            DisplayName = publication.Descriptor.DisplayName,
            Version = publication.Descriptor.Version,
        };
    }

    private static ActionSchemaDetailResponse MapDetail(ActionPublication publication) =>
        new()
        {
            Descriptor = MapDescriptor(publication.Descriptor),
            Schema = new ActionSchemaBundleDto
            {
                InputSchema = publication.SchemaBundle.InputSchema.RootElement.Clone(),
                OutputSchema = publication.SchemaBundle.OutputSchema.RootElement.Clone(),
                SchemaVersion = publication.SchemaBundle.SchemaVersion,
            },
            UiMetadata = publication.UiMetadata is null ? null : MapUiMetadata(publication.UiMetadata),
        };

    private static ActionSchemaDescriptorDto MapDescriptor(PublicationActionDescriptor descriptor) =>
        new()
        {
            ActionId = descriptor.ActionId,
            Version = descriptor.Version,
            DisplayName = descriptor.DisplayName,
            Description = descriptor.Description,
            Category = descriptor.Category,
            Icon = descriptor.Icon,
            DocumentationUrl = descriptor.DocumentationUrl,
            Tags = descriptor.Tags ?? [],
            Examples = descriptor.Examples?
                .Select(example => new ActionExampleDto
                {
                    Title = example.Title,
                    Input = example.Input.RootElement.Clone(),
                })
                .ToList(),
        };

    private static ActionUiMetadataDto MapUiMetadata(ActionUiMetadata metadata) =>
        new()
        {
            FieldOrder = metadata.FieldOrder,
            EnumLabelKeys = metadata.EnumLabelKeys,
            Fields = metadata.Fields?
                .ToDictionary(
                    static pair => pair.Key,
                    static pair => new ActionFieldUiHintsDto
                    {
                        Widget = pair.Value.Widget,
                        LabelKey = pair.Value.LabelKey,
                        DescriptionKey = pair.Value.DescriptionKey,
                        PlaceholderKey = pair.Value.PlaceholderKey,
                        Sensitive = pair.Value.Sensitive,
                    },
                    StringComparer.Ordinal),
        };
}
