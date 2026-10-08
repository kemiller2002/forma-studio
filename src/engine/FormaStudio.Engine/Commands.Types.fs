namespace FormaStudio.Engine

type IncidentEdgePolicy =
    | RejectIfConnected
    | RemoveIncidentEdges

type PaletteRemoval =
    | BlockPaletteIfUsed
    | ReassignPalette of PaletteSlotId
    | MaterializePalette

type StyleRemoval =
    | BlockStyleIfUsed
    | DetachAndMaterialize

type FieldRemoval =
    | BlockFieldIfUsed
    | RemoveFieldValues

type SlotPosition = { Parent: ComponentNodeId; Slot: string }

type LayoutCommand =
    | AddPage of PageId * name: string * route: string option
    | AddComponent of PageId * parent: SlotPosition option * index: int * ComponentNodeId * componentId: string
    | RemoveComponent of PageId * ComponentNodeId
    | MoveComponent of PageId * ComponentNodeId * parent: SlotPosition option * index: int
    | SetComponentProperty of PageId * ComponentNodeId * name: string * value: JsonValue option
    | SetComponentContent of PageId * ComponentNodeId * key: string * text: string

type FlowCommand =
    | AddDiagram of DiagramId * name: string * ProfileRef
    | RenameDiagram of DiagramId * name: string
    | RemoveDiagram of DiagramId
    | AddNode of DiagramId * NodeId * kind: string * label: string * x: float * y: float * width: float * height: float
    /// Moves several nodes by one delta each as one history entry (FDA-063).
    | MoveNodes of DiagramId * (NodeId * float * float) list
    | ResizeNode of DiagramId * NodeId * width: float * height: float
    | SetNodeLabel of DiagramId * NodeId * string
    | SetNodeKind of DiagramId * NodeId * string
    | RemoveNode of DiagramId * NodeId * IncidentEdgePolicy
    | Connect of DiagramId * EdgeId * kind: string * source: Endpoint * target: Endpoint * label: string option
    | Reconnect of DiagramId * EdgeId * source: Endpoint * target: Endpoint
    | SetEdgeLabel of DiagramId * EdgeId * string option
    | SetEdgeRouting of DiagramId * EdgeId * Routing
    | RemoveEdge of DiagramId * EdgeId
    | AddGroup of DiagramId * GroupId * GroupKindName * label: string * x: float * y: float * width: float * height: float
    /// Moves a container and its explicit members together (FDA-103).
    | MoveGroup of DiagramId * GroupId * dx: float * dy: float
    /// Places a node in one lane (or none); lanes are exclusive (FDA-029, FDA-102).
    | AssignLane of DiagramId * NodeId * GroupId option
    | SetGroupMembers of DiagramId * GroupId * NodeId list
    | SetDisplay of DiagramId * MetadataDisplay
    | AddReference of ObjectRef * TypedReference
    | RemoveReference of ObjectRef * ReferenceId

type MetadataCommand =
    | DefineField of FieldDefinition
    | RenameField of FieldKey * name: string
    | SetFieldDisclosure of FieldKey * Disclosure
    | RemoveField of FieldKey * FieldRemoval
    /// Sets one field on several objects atomically (FDA-983).
    | SetValue of ObjectRef list * FieldKey * StoredValue
    /// Removes the stored value, revealing the default or derived value (FDA-984).
    | ClearValue of ObjectRef list * FieldKey
    /// Turns a default, derived or source-bound value into an explicit one (FDA-928).
    | MaterializeValue of ObjectRef list * FieldKey

type AppearanceCommand =
    | AddPaletteSlot of PaletteSlot
    | RenamePaletteSlot of PaletteSlotId * name: string
    | SetPaletteValue of PaletteSlotId * PaletteValue
    | MovePaletteSlot of PaletteSlotId * index: int
    | RemovePaletteSlot of PaletteSlotId * PaletteRemoval
    | DefineStyle of AppearanceStyle
    | UpdateStyle of StyleId * Appearance
    | RenameStyle of StyleId * name: string
    | RemoveStyle of StyleId * StyleRemoval
    | ApplyStyle of ObjectRef list * StyleId option
    /// Sets the properties that are Some in the given appearance as overrides.
    | SetOverride of ObjectRef list * Appearance
    /// Removes overrides so the next layer shows through (FDA-1064).
    | ResetOverride of ObjectRef list * AppearanceProperty list
    | DetachStyle of ObjectRef list
    | DefineMapping of PresentationMapping
    | UpdateMapping of PresentationMapping
    | RemoveMapping of MappingId
    | MoveMapping of MappingId * index: int
    /// Sets or removes the Forma icon name of a Layout component or diagram node.
    | SetIcon of ObjectRef * IconName option

/// One typed command surface for Layout, Flow, metadata and appearance. Pointer,
/// keyboard, inspector, Structure view and agent input all produce these values
/// (FDA-069, FDA-221, FDA-1020).
type Command =
    | Layout of LayoutCommand
    | Flow of FlowCommand
    | MetadataCmd of MetadataCommand
    | AppearanceCmd of AppearanceCommand
    | Batch of label: string * Command list

type Applied = { Project: Project; Obligations: Obligation list }
