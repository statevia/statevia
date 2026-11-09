namespace Statevia.Service.Api.Application.Definition;

/// <summary>
/// nodes 形式（UI スキーマ・<see cref="NodesWorkflowDefinitionLoader"/> 共通）の JSON キー・型名・列挙値。
/// </summary>
internal static class NodesSchemaLiteral
{
    /// <summary>JSON Schema の型名 <c>object</c>。</summary>
    public const string TypeObject = "object";

    /// <summary>JSON Schema の型名 <c>string</c>。</summary>
    public const string TypeString = "string";

    /// <summary>JSON Schema の型名 <c>integer</c>。</summary>
    public const string TypeInteger = "integer";

    /// <summary>JSON Schema の型名 <c>array</c>。</summary>
    public const string TypeArray = "array";

    /// <summary>JSON Schema の型名 <c>boolean</c>。</summary>
    public const string TypeBoolean = "boolean";

    /// <summary>JSON Schema の型名 <c>number</c>。</summary>
    public const string TypeNumber = "number";

    /// <summary>JSON Schema の型名 <c>null</c>。</summary>
    public const string TypeNull = "null";

    /// <summary>JSON のキー <c>version</c>。</summary>
    public const string KeyVersion = "version";

    /// <summary>JSON のキー <c>workflow</c>。</summary>
    public const string KeyWorkflow = "workflow";

    /// <summary>JSON のキー <c>nodes</c>。</summary>
    public const string KeyNodes = "nodes";

    /// <summary>JSON のキー <c>controls</c>。</summary>
    public const string KeyControls = "controls";

    /// <summary>JSON のキー <c>id</c>。</summary>
    public const string KeyId = "id";

    /// <summary>JSON のキー <c>type</c>。</summary>
    public const string KeyType = "type";

    /// <summary>JSON のキー <c>name</c>。</summary>
    public const string KeyName = "name";

    /// <summary>JSON のキー <c>next</c>。</summary>
    public const string KeyNext = "next";

    /// <summary>JSON のキー <c>error</c>。</summary>
    public const string KeyError = "error";

    /// <summary>JSON のキー <c>action</c>。</summary>
    public const string KeyAction = "action";

    /// <summary>JSON のキー <c>event</c>。</summary>
    public const string KeyEvent = "event";

    /// <summary>JSON のキー <c>events</c>。</summary>
    public const string KeyEvents = "events";

    /// <summary>JSON のキー <c>assignees</c>。</summary>
    public const string KeyAssignees = "assignees";

    /// <summary>JSON のキー <c>subscribe</c>。</summary>
    public const string KeySubscribe = "subscribe";

    /// <summary>JSON のキー <c>topic</c>。</summary>
    public const string KeyTopic = "topic";

    /// <summary>JSON のキー <c>key</c>。</summary>
    public const string KeyKey = "key";

    /// <summary>JSON のキー <c>exits</c>。</summary>
    public const string KeyExits = "exits";

    /// <summary>JSON のキー <c>branches</c>。</summary>
    public const string KeyBranches = "branches";

    /// <summary>JSON のキー <c>input</c>。</summary>
    public const string KeyInput = "input";

    /// <summary>JSON のキー <c>output</c>。</summary>
    public const string KeyOutput = "output";

    /// <summary>JSON のキー <c>mode</c>。</summary>
    public const string KeyMode = "mode";

    /// <summary>JSON のキー <c>edges</c>。</summary>
    public const string KeyEdges = "edges";

    /// <summary>JSON のキー <c>to</c>。</summary>
    public const string KeyTo = "to";

    /// <summary>JSON のキー <c>when</c>。</summary>
    public const string KeyWhen = "when";

    /// <summary>JSON のキー <c>path</c>。</summary>
    public const string KeyPath = "path";

    /// <summary>JSON のキー <c>op</c>。</summary>
    public const string KeyOp = "op";

    /// <summary>JSON のキー <c>order</c>。</summary>
    public const string KeyOrder = "order";

    /// <summary>JSON のキー <c>default</c>。</summary>
    public const string KeyDefault = "default";

    /// <summary>JSON のキー <c>edges[].to</c>。</summary>
    public const string KeyEdgesTo = "edges.to";

    /// <summary>ノードの種別 <c>start</c>。</summary>
    public const string NodeTypeStart = "start";

    /// <summary>ノードの種別 <c>end</c>。</summary>
    public const string NodeTypeEnd = "end";

    /// <summary>ノードの種別 <c>action</c>。</summary>
    public const string NodeTypeAction = "action";

    /// <summary>ノードの種別 <c>wait</c>。</summary>
    public const string NodeTypeWait = "wait";

    /// <summary>ノードの種別 <c>fork</c>。</summary>
    public const string NodeTypeFork = "fork";

    /// <summary>ノードの種別 <c>join</c>。</summary>
    public const string NodeTypeJoin = "join";

    /// <summary>Join のモード <c>all</c>。</summary>
    public const string JoinModeAll = "all";
}
