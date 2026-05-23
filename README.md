# 1. Meta‑Specification: The System Describing Itself

The engine’s own abstract syntax is described using the same kind of definitions. This self‑description serves as the **bootstrap specification**: it allows the engine to interpret the very definitions that constitute it, enabling validation, tooling, and evolution.

The system described here is a specification‑only engine. It defines the static shape of types, rules, and values, and the normative rules for decomposing natural‑language subjects into structured interpretations. The actual execution of `ref` interpretations and user‑defined actions is delegated to an external, black‑box runtime; this specification does not prescribe how that runtime works.

## 1.1 Meta‑Types

The following record types are part of the engine's built‑in knowledge. They define the structure of a specification. All record fields are **mandatory**; the full shape of any instance is given by the combination of these fields and the type's annotation.

@"A generic tuple of two values"
record Tuple<t1, t2> with val1(t1), val2(t2)

@"Group an array of objects by equivalance of a given key, returns the dictionary of groups"
action groupBy<t> on array(t[]), groupKey(literal) returns record Tuple<literal, t[]>[]

@"Find a dictionary value of the given key"
action findInDictionary<tk, tv> on dictionary(record Tuple<tk, tv>[]), key(tk) returns tv

@"A type spec reference. It only has a literal string *typeId*, which must be of `type-id := base-id ("[]")?`, and `base-id = "literal" | ("record" record-id generic-params) | type-id`. Here generic params are passed as `"<"t1,t2,...">"` if needed, and `t1,t2` are again type identifiers or generic names provided a generic context."
record TypeSpec with typeId(literal)

*Note: generic params not considered*
@"A record definition. It has a unique identifier *id*, an array *fields* of the full set of mandatory fields (a key-type typle for each), and an annotation that is the complete structural contract of the type (including any grammar syntax when applicable)."
record RecordDef with
  annotation(literal), id(literal), fields(record Tuple<literal, record TypeSpec>[])

@"A value expression appearing in a rule, represented as a literal field whose value is a string in the ValueExpr DSL.
  The DSL is a line‑based format. Each line is one of:
    - `var <name> [.<field> ...]`: variable reference with optional field path.
    - `"<template>"`: interpolated string.
    - `ref(<type>) "<subject>"`: sub‑interpretation. `<subject>` is a natural‑language noun phrase that describes the value the system is to construct; `<type>` is the expected type for the product.
    - `array [ <elem1>, <elem2>, ...]`: array literal.
    - `record <typeId> with <field1> = <expr>, <field2> = <expr>, ...`: record construction.
    - `satisfying <ruleId> where <arg1> = <expr>, ...`: definite rule call.
    - `result of <actionName> where <arg1> = <expr>, ...`: action call.
    - `seq <innerKind> <arg1> = (iter)? <expr>, ...`: sequential evaluation, where innerKind is `record...with`, `satisfying...where`, or `result of...where`, and `iter` is an optional keyword for expressions.  
  All expressions that appear as arguments inside another expression are themselves represented as DSL strings."
record ValueExpr with dslText(literal)

@"A rule definition. Contains an id, an annotation, its formal parameters (for-block), local bindings (given-block), and the final value expression (must-be)."
record RuleDef with
  id(literal), annotation(literal),
  forParams(record Tuple<literal, record TypeSpec>[]),
  givenBindings(record Tuple<literal, record ValueExpr>[]),  
  mustBe(record ValueExpr)

@"An action definition. Contains the action's name, annotation, formal parameters, output type, and an actual Lua script implementation of the action."
record ActionDef with
  name(literal), annotation(literal),
  params(record Tuple<literal, record TypeSpec>[]),
  outputType(record TypeSpec),
  luaScript(literal)

@"An add-record operation. It contains the complete RecordDef to be added to the ruleset."
record AddRecordDef with unit(record RecordDef)

@"An add-rule operation. It contains the complete RuleDef to be added to the ruleset."
record AddRuleDef with unit(record RuleDef)

@"An add-action operation. It contains the complete ActionDef to be added to the ruleset."
record AddActionDef with unit(record ActionDef)

@"A removal operation. It identifies a unit of a given type (among 'rule'|'record'|'action') to be removed by its unique id."
record RemoveRulesetUnit with type(literal), unitId(literal)

@"A replace-record operation. It provides the new full RecordDef whose id is the id of the target record to be replaced"
record ReplaceRecordDef with newUnit(record RecordDef)

@"A replace-rule operation. It provides the new full RuleDef whose id is the id of the target rule to be replaced."
record ReplaceRuleDef with newUnit(record RuleDef)

@"A replace-action operation. It provides the new full ActionDef whose id is the id of the target action to be replaced."
record ReplaceActionDef with newUnit(record ActionDef)

@"A structured plan that collects all atomic modification operations generated from a single change request. Each field is an array of one specific operation type. The arrays together fully describe the intended transformation of the ruleset."
record ModificationPlan with
  addRecords(record AddRecordDef[]),
  addRules(record AddRuleDef[]),
  addActions(record AddActionDef[]),
  removeUnits(record RemoveRulesetUnit[]),
  replaceRecords(record ReplaceRecordDef[]),
  replaceRules(record ReplaceRuleDef[]),
  replaceActions(record ReplaceActionDef[])

@"Modification Root: Interprets a natural‑language description of changes to the ruleset into a concrete ModificationPlan containing only explicit add, remove, and replace operations. This rule must be used whenever an input implies altering the ruleset's content. It prevents ad‑hoc bundling of multiple changes into a single rule and ensures every atomic modification is explicitly represented."
rule modify-ruleset for changeDescription(literal)
given:
  breakdown: ref(record ChangeItem[]) "the structured classification of the change request '{changeDescription}', identifying each individual modification item as an add, remove, or replace, and specifying the target type (record, rule, or action)"
  addRecords: ref(record AddRecordDef[]) "all new RecordDefs that must be added, each interpreted from the corresponding add‑record items in {breakdown}"
  addRules: ref(record AddRuleDef[]) "all new RuleDefs that must be added, each interpreted from the corresponding add‑rule items in {breakdown}"
  addActions: ref(record AddActionDef[]) "all new ActionDefs that must be added, each interpreted from the corresponding add‑action items in {breakdown}"
  removeUnits: ref(record RemoveRulesetUnit[]) "all unit ids that must be removed, directly extracted from the remove items in {breakdown}"
  replaceRecords: ref(record ReplaceRecordDef[]) "all replacement RecordDefs, each synthesised by taking the old definition (known from the ruleset by id) and applying the described modifications for the replace‑record items in {breakdown}"
  replaceRules: ref(record ReplaceRuleDef[]) "all replacement RuleDefs, each synthesised analogously for replace‑rule items in {breakdown}"
  replaceActions: ref(record ReplaceActionDef[]) "all replacement ActionDefs, each synthesised analogously for replace‑action items in {breakdown}"
must be:
  record ModificationPlan with
    addRecords = addRecords,
    addRules = addRules,
    addActions = addActions,
    removeUnits = removeUnits,
    replaceRecords = replaceRecords,
    replaceRules = replaceRules,
    replaceActions = replaceActions

@"A single atomic change item extracted from a description. It indicates the operation (among 'add'|'remove'|'replace'), the target unit type('record'|'rule'|'action'), the target unit id (for adds this may be the intended new id or empty if it must be inferred), and the natural‑language description of the change payload (empty for removal)."
record ChangeItem with
  operation(literal),
  unitType(literal),
  unitId(literal),
  payload(literal)

@"Interpret Change Item: Given a natural‑language phrase, produce a single ChangeItem if the phrase describes exactly one atomic modification to the ruleset
  An atomic modification is defined by the following criteria:
  - It refers to exactly one target unit, identified by its id (for an existing unit) or by a clear new name (for a new unit).
  - It expresses exactly one operation intent: 'add' (introduce a new unit), 'remove' (delete an existing unit by id), or 'replace' (modify an existing unit by providing a new full definition, which incorporates the described changes).
  - The phrase must not contain implicit sequencing (e.g., 'first add X then remove Y') or combined actions on multiple units (e.g., 'add X and update Y'). If such composite intent is present, the phrase must be split before applying this rule."
rule interpret-change-item for phrase(literal)
given:
  operation: ref(literal) "the operation kind: 'add', 'remove', or 'replace', inferred from the phrase '{phrase}'"
  unitType: ref(literal) "the unit type: 'record', 'rule', or 'action', inferred from the phrase and the context of what is being modified"
  unitId: ref(literal) "the id of the affected unit; for an add, this is the proposed new id (may be empty if it must be generated later); for remove or replace, it is the existing id"
  payload: ref(literal) "for add and replace, the natural‑language description of what the new definition should contain; for remove, this must be empty"
must be:
  record ChangeItem with
    operation = operation,
    unitType = unitType,
    unitId = unitId,
    payload = payload

@"Helper"
record RuleOutline with id(literal), annotation(literal)

@"Interprets a natural language description of a rule definition and returns a RuleDef node.
The description must encode knowledge that cannot be mechanically derived from the structure of its output.
Trivial extractions (e.g., directly returning a field from the input without additional constraints) are forbidden."
rule interpret-rule-def for rawText(literal)
given:
  outline: ref(record RuleOutline) "the rule identifier and its annotation extracted from the text: {rawText}"
  forParams: ref(record Tuple<literal, record TypeSpec>[]) "the array of formal parameter declarations (for‑block) found in: {rawText}"
  givenBindings: ref(record Tuple<literal, record ValueExpr>[]) "the array of given bindings found in: {rawText}"
  mustBeText: ref(literal) "the natural‑language phrase that describes the 'must be' value expression in: {rawText}"
  mustBe: ref(record ValueExpr) "the ValueExpr node corresponding to the value expression phrase: {mustBeText}"
must be:
  record RuleDef with
  id = outline.id, annotation = outline.annotation,
  forParams = forParams, givenBindings = givenBindings, mustBe = mustBe

@"Interprets the annotation for a rule. The annotation must contain subject contract that the rule declares to its callers: it must describe the characteristics of a subject that the rule is legitimately capable of interpreting, allowing caller to organise the subject into a compliant form before the call."
rule interpret-rule-annotation for rawText(literal)
must be: ref(literal) "A rule annotation for {$for.rawText}. It must describe/constrain its interpretation subject, which allows the referer to know \"what subject should be passed so that this rule can interpret it\"."

@"Interprets a natural‑language description of a ref subject, where the ref subject must be declarative (not some command/call) and undetermined, that is, not containing any explicit reference to any definite rule/action existing in the ruleset. The rule produces a ValueExpr node containing the corresponding DSL string 'ref(<type>) \"<subject>\"'."
rule interpret-ref-call for refDescription(literal)
given:
  subjectPart: ref(literal) "the noun phrase that describes the value to be constructed, as contained in the ref call description: {refDescription}"
  expectedType: ref(literal) "the type name given after 'as' in the ref call description: {refDescription}"
  dsl = ref(literal) "ref({expectedType}) \"{subjectPart}\""
must be: record ValueExpr with dslText = dsl

@"Given a natural language phrase describing a value expression, produce a ValueExpr node. When the phrase describes a ref sub‑interpretation, the rule delegates to interpret-ref-call to enforce the subject‑style form. Otherwise, it wraps the phrase into a DSL string following the ValueExpr DSL specification."
rule interpret-value-expr for phrase(literal)
given:
  isRefCall: ref(literal) "whether the phrase {phrase} describes a ref sub‑interpretation (i.e., it contains an 'as' clause and the leading part describes a value)"
  refDslText: ref(record ValueExpr) "if {isRefCall} is true, interpret the phrase as a ref call using interpret-ref-call with refDescription: {phrase}; otherwise return an empty ValueExpr (dslText='')"
  defaultDslText: ref(literal) "if {isRefCall} is false, the ValueExpr DSL string that represents the natural language phrase '{phrase}', following the DSL specification"
  dslText: ref(literal) "if {isRefCall} is true, use the dslText from {refDslText}; otherwise use {defaultDslText}"
must be: record ValueExpr with dslText = dslText

@"Helper"
record TypeBasics with name(literal), rawAnnotation(literal)

@"Interprets a natural language description of a record definition only with structure/shape, not as a rule/standard source. Returns a RecordDef node. The process extracts the mandatory fields and synthesizes the complete shape annotation, which may incorporate a grammar specification"
rule interpret-record-def for rawText(literal)
given:
  basics: ref(record TypeBasics) "the record name and the raw annotation paragraph contained in the text: {rawText}"
  fieldDecls: ref(record Tuple<literal, literal>[]) "all mandatory field declarations (each with a name and a type description) found in: {rawText}"
  fieldTypes: ref(record TypeSpec[]) "the field definitions with each type fixed as a type spec reference that specifies the type description, transformed from {fieldDecls.val2}"
  constructedFields: seq record Tuple<literal, record TypeSpec> with val1=iter fieldDecls.val1, val2=iter fieldTypes
  canonicalAnnotation: ref(literal) "the complete shape annotation for type {basics.name}, which refines and formalizes the raw annotation '{basics.rawAnnotation}', incorporates the fields {fieldDecls}, and covers any mentioned DSL specification. It is a self-contained description of the type's structure, field contracts, and, if applicable, the DSL grammar and semantics."
must be:
  record RecordDef with
    id = basics.name, fields = constructedFields, annotation = canonicalAnnotation

@"A compact description of a single ruleset unit, containing its unique id, its unit type (\"record\"|\"rule\"|\"action\"), and its natural language annotation."
record UnitSummary with
  id(literal), unitType(literal), annotation(literal)

record RulesetQuery with
  recordIds: literal[]
  ruleIds: literal[]
  actionIds: literal[]

record RulesetSection with
  records: RecordDef[]
  rules: RuleDef[]
  actions: ActionDef[]

action retrieveSimilarUnits on query(literal) returns record UnitSummary[]

action findSectionInRuleset on query(record RulesetQuery) returns record RulesetSection

@"Interprets a natural‑language phrase that describes an expectation about desired units in the ruleset.
It returns an array of UnitSummary entries for the ruleset units that are semantically relevant to that expectation."
rule interpret-relevant-units for expectationPhrase(literal)
given:
  rawSummaries: result of retrieveSimilarUnits where query = expectationPhrase
  groupedSummaries: result of groupBy<record UnitSummary> where array = rawSummaries, groupKey = "unitType"
  recordSummaries: result of findInDictionary<literal, record UnitSummary[]> where dictionary = groupedSummaries, key = "record"
  ruleSummaries: result of findInDictionary<literal, record UnitSummary[]> where dictionary = groupedSummaries, key = "rule"
  actionsSummaries: result of findInDictionary<literal, record UnitSummary[]> where dictionary = groupedSummaries, key = "action"
  rulesetQuery: ref(record RulesetQuery) "the ids of ruleset units within records: {recordSummaries}, rules: {ruleSummaries}, actions: {actionSummaries}, which need their details queried to meet the expectation: {expectationPhrase}"
must be: result of findSectionInRuleset where query = rulesetQuery

@"Interprets a literal project items expectation into a literal expectation of a ruleset item"
rule interpret-project-items for expectationPhrase(literal)
must be:
  rulesetExp = ref(literal) "an expectation literal of existing ruleset items which fully describe the expectation for project items: {expectationPhrase}"
