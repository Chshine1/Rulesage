# 1. Meta‑Specification: The System Describing Itself

The system described here is a specification‑only engine. It defines the static shape of types, rules, and values, and the normative rules for decomposing natural‑language subjects into structured interpretations. The actual execution of `ref` interpretations and user‑defined actions is delegated to an external, black‑box runtime; this specification does not prescribe how that runtime works.

It only does "interpretation", and modifications like "replace a rule" are also interpreted, like interpreted to a standardized modification node.

## Meta‑Ruleset

```text
@"A generic tuple of two values"
record Tuple<t1, t2> with val1(t1), val2(t2)

@"Group an array of objects by equivalance of a given key, returns the dictionary of groups"
action groupBy<t> on array(t[]), groupKey(literal) returns record Tuple<literal, t[]>[]

@"Find a dictionary value of the given key"
action findInDictionary<tk, tv> on dictionary(record Tuple<tk, tv>[]), key(tk) returns tv

@"A type spec reference. It only has a literal string *typeId*, which must be of `type-id := base-id ("[]")?`, and `base-id = "literal" | ("record" record-id generic-params) | type-id`. Here generic params are passed as `"<"t1,t2,...">"` if needed, and `t1,t2` are again type identifiers or generic names provided a generic context."
record TypeSpec with typeId(literal)

@"An add-record operation. It contains the complete RecordDef to be added to the ruleset."
record AddRecordDef with unit(record RecordDef)

@"An add-rule operation. It contains the complete RuleDef to be added to the ruleset."
record AddRuleDef with unit(record RuleDef)

@"An add-action operation. It contains the complete ActionDef to be added to the ruleset."
record AddAction with unit(record ActionDef)

@"A removal operation. It identifies a unit of a given type (among 'rule'|'record'|'action') to be removed by its unique id."
record RemoveRulesetUnit with type(literal), unitId(literal)

@"A replace-record operation. It provides the new full RecordDef whose id is the id of the target record to be replaced"
record ReplaceRecordDef with newUnit(record RecordDef)

@"A replace-rule operation. It provides the new full RuleDef whose id is the id of the target rule to be replaced."
record ReplaceRuleDef with newUnit(record RuleDef)

@"A replace-action operation. It provides the new full ActionDef whose id is the id of the target action to be replaced."
record ReplaceActionDef with newUnit(record ActionDef)

@"A compact description of a single ruleset unit, containing its unique id, its unit type (\"record\"|\"rule\"|\"action\"), and its natural language annotation."
record UnitSummary with
  id(literal), unitType(literal), annotation(literal)

@"A collection of ids (of records/rules/actions, respectively), which describes a query against the quleset requiring units of these ids"
record RulesetQuery with
  recordIds: literal[]
  ruleIds: literal[]
  actionIds: literal[]

@"Find units in the ruleset by semantic similarity to a literal query, return the similar units as an array of record UnitSummary"
action retrieveSimilarUnits on query(literal) returns record UnitSummary[]

**Question: Generic parameters?**
@"Interprets a natural language type description phrase into a TypeSpec."
rule interpret-type-spec for typeDesc(literal)
given:
  availableUnits: result of retrieveSimilarUnits where query = typeDesc
  groupedUnits: result of groupBy<record UnitSummary> where array = availableUnits, groupKey = "unitType"
  availableRecords: result of findInDictionary<literal, record UnitSummary[]> where dictionary = groupedUnits, key = "record"
  baseId: ref(literal) "the base type identifier that corresponds to the type described by '{typeDesc}'. It must be one of 'literal', or a record id in {availableRecords}, or a generic parameter name."
  arrayLayers: ref(literal) "the number of array layers, for example, 0 for `literal`, \"an array of strings\" should be 1, and \"an array of string arrays\" is also allowed, having layer 2."
must be: ref(TypeSpec) "the fully assembled type id string, composed of {baseId}, having {arrayLayers} layers of array represented by appending that number of suffixes '[]'"
```

### 1. Rules

```text
@"A rule definition. Contains an id, an annotation, its formal parameters (for-block), local bindings (given-block), and the final value expression (must-be)."
record RuleDef with
  id(literal), annotation(literal),
  forParams(record Tuple<literal, record TypeSpec>[]),
  givenBindings(record Tuple<literal, record ValueExpr>[]),  
  mustBe(record ValueExpr)

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
```

---

```text
"Interprets a natural-language text that describes exactly one rule definition into a RuleDef
It must be declarative and interprative: it states what will be interpreted to what a standardized form, not the procedure to execute something.
It should describe a subject to be interpreted, which may be parametrized among a whole subject type, then steps of intermediate interpretations/actions/record constructions. They should finally build to a standardized instance, which is \"what this subject will be interpreted to\".
The constraint can be put on the subject for the interpretation to run at better performance
The description shouldn't define new record shapes/actions inline. If also shouldn't just be a trivial extraction like output merely copies an input field; The interpretation must add structural/domain constraints, etc."
rule interpret-rule-def for rawText(literal)
given:
  subject: satisfying identify-def-subject where description = rawText
  rawAnnotation:
    ref(literal) "the natural language annotation for this rule, describing the subject contract, taken from: {rawText}"
  annotation: satisfying canonicalize-annotation where
    rawAnnot = rawAnnotation, subjectType = "rule", subjectId = ruleId, details = ""
  rawForParams:
    ref(record Tuple<literal, literal>[]) "the formal parameters of the rule as described in: {rawText}. Each is a pair: parameter name and its natural language type description."
  forParamTypes: seq satisfying interpret-type-spec where typeDesc = iter rawForParams.val2
  typedForParams: seq record Tuple<literal, record TypeSpec> with
    val1 = iter rawForParams.val1,
    val2 = iter forParamTypes
  rawGivenBindings:
    ref(record Tuple<literal, literal>[]) "the intermediate bindings described in: {rawText}, where each binding has a name and a natural language expression description."
  givenBindingTypes: seq satisfying interpret-type-spec where typeDesc = iter rawGivenBindings.val2
  givenBindings: seq record Tuple<literal, record ValueExpr> with
    val1 = iter rawGivenBindings.val1,
    val2 = iter givenBindingTypes
  mustBePhrase:
    ref(literal) "the natural language description of the final value expression (the 'must be' part) in: {rawText}"
  mustBe: satisfying interpret-value-expr where phrase = mustBePhrase
must be:
  record RuleDef with
    id = ruleId,
    annotation = annotation,
    forParams = typedForParams,
    givenBindings = givenBindings,
    mustBe = mustBe
```

### 2. Records

```text
*Note: generic params should also be included*
@"A record definition. It has a unique identifier *id*, an array *fields* of the full set of mandatory fields (a key-type typle for each), and an annotation that is the complete structural contract of the type (including any grammar syntax when applicable)."
record RecordDef with
  annotation(literal), id(literal), fields(record Tuple<literal, record TypeSpec>[])

@"Interprets a natural-language text that describes exactly one record type definition into a standardized RecordDef
The description must declare all mandatory fields with their names and a detailed type and structural description. And it should be purely structural and declarative: describing what shape the data has, not how it is computed or when it is used
It shouldn't contain any behavioural rules, any executable action definitions."
rule interpret-record-def for rawText(literal)
given:
  subject: satisfying interpret-def-subject where description = rawText
  rawAnnotation:
    ref(literal) "the natural language description that serves as the annotation for the record '{subject}', taken from: {rawText}. It should describe the purpose and contract of this record type."
  fieldDescs: satisfying interpret-fields where recordDescription = rawText
  fieldTypes: seq satisfying interpret-type-spec where typeDesc = iter fieldDescs.val2
  constructedFields:
    seq record Tuple<literal, record TypeSpec> with
      val1 = iter fieldDescs.val1,
      val2 = iter fieldTypes
  detailsForAnnot:
    ref(literal) "a structured summary of the fields: {constructedFields}"
  canonicalAnnotation: satisfying canonicalize-annotation where
    rawAnnot = rawAnnotation, subjectType = "record",
    subjectId = recordId, details = detailsForAnnot
must be:
  record RecordDef with
    id = recordId,
    fields = constructedFields,
    annotation = canonicalAnnotation
```

### 3. Actions

```text
@"An action definition. Contains the action's name, annotation, formal parameters, output type, and an actual Lua script implementation of the action."
record ActionDef with
  name(literal), annotation(literal),
  params(record Tuple<literal, record TypeSpec>[]),
  outputType(record TypeSpec),
  luaScript(literal)

@"Interprets a natural-language text that describes exactly one action definition into an ActionDef.
The description must describe an \"action\", which is to be applied to a set of typed parameters.
Then describe a domain-indepentent, atomic operation on these params which does not contain any semantic logic, any rule/action call, and isn't a simple action composable from field accessing/record construction. This needs to produce a final result.
It shouldn't describe a record shape or a behavioural rule, nor do rule‑specific interpretations."
rule interpret-action-def for rawText(literal)
given:
  subject: satisfying identify-def-subject where description = rawText
  rawAnnotation: ref(literal) "the natural language annotation for the action, taken from: {rawText}"
  annotation: satisfying canonicalize-annotation where
    rawAnnot = rawAnnotation, subjectType = "action", subjectId = actionName, details = ""
  rawParams:
    ref(record Tuple<literal, literal>[]) "the formal parameters described in: {rawText}"
  paramTypes: seq satisfying interpret-type-spec where typeDesc = iter rawParams.val2
  typedParams: seq record Tuple<literal, record TypeSpec> with
    val1 = iter rawParams.val1,
    val2 = iter paramTypes
  outputTypeDesc:
    ref(literal) "the natural language description of the output type, as stated in: {rawText}"
  outputType: satisfying interpret-type-spec where typeDesc = outputTypeDesc
  operationDesc:
    ref(literal) "the pure, domain-independent operation description extracted from: {rawText}. This description should state what the action does without referencing business logic."
  luaScript:
    ref(literal) "a Lua script that implements an action with name '{actionName}', parameters {typedParams}, output type {outputType}, and operation: '{operationDesc}'"
must be:
  record ActionDef with
    name = subject,
    annotation = annotation,
    params = typedParams,
    outputType = outputType,
    luaScript = luaScript
```

---

```text
@"Describe a section in the ruleset, containing the full definitions or records/rules/actions, respectively."
record RulesetSection with
  records: RecordDef[]
  rules: RuleDef[]
  actions: ActionDef[]

@"From a RulesetQuery which describes the desired ids, find the corresponding RulesetSection matching these query ids."
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
```