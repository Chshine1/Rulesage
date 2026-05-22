# Normative Specification Engine

## System Overview

The system is a **normative specification engine** that transforms natural‑language statements about domain entities into structured, typed, and verifiable representations. The engine is guided by a network of user‑defined **rules**, **types**, and deterministic **actions**, whose annotations form a gradually evolving, machine‑interpretable specification of the target domain.

The system is designed to be **self‑describing**: its own specification (the concepts of *type*, *rule*, *value expression*, etc.) is expressed using the same kind of rules and types that it exposes to users. This makes it possible to validate, extend, and even partially generate the engine’s own behaviour through its own mechanisms.

---

# 1. Core Concepts

## 1.1 Subject

A **subject** is any entity, concept, or value that the system seeks to describe, constrain, or construct in a standardized form. A subject is presented as a piece of natural language, optionally together with concrete parameter values (when it appears as part of a rule invocation).

- Example subjects:
    - `"a User with name 'John' and age 30"`
    - `"the list of all active orders"`
    - `"for a raw name and a raw age, construct a user record"` (i.e., a rule definition)

- The engine’s job is to *interpret* a subject into a **node** that conforms to an expected **type**, using all available rules, annotations, and the interpretation context.

## 1.2 Type

A **type** defines the *shape* that a value must conform to. Every type is exactly one of:

- **`literal`** – the type of primitive string values. Its annotation is system‑fixed: *“a plain string value”*.
- **`record <id>`** – a user‑defined structural type, optionally parameterized with type identifiers (e.g., `record tuple<t1, t2>`). A record type declares a fixed set of **fields**; each field has a name and a type (which itself may be `literal`, a record type, or an array type; recursion is forbidden).  
  Every record type **must** carry a **user‑defined annotation** that describes its shape constraints beyond the bare field declarations (e.g., “a user with a non‑empty name and a positive age”).
- **`array`** – formed by appending `[]` to a base type (e.g., `literal[]`, `record user[][]`). Its annotation is system‑fixed: *“an ordered collection of elements, each conforming to the element type”*.

**Annotations are part of the type’s shape contract.** For `literal` and `array` the annotation is immutable and provided by the system; for `record` it is required and supplied by the user. An annotation never prescribes *how* to construct a value; it only declares what a valid instance looks like.

Additionally, each **rule** and each **action** carries an annotation describing its purpose, expected inputs, and outputs. The annotation is the primary information visible to the LLM when rules/actions are retrieved into an interpretation context.

## 1.3 Node

A **node** is a concrete value that instantiates a type. Its form depends entirely on that type:

- If the type is `literal`, the node is a plain string (e.g. `"John"`).
- If the type is a record type, the node is a structured object with a value for every declared field. Example: for a type `record tuple<literal, literal>` with fields `val1` and `val2`, a node might be `tuple with val1="hello", val2="world"`.
- If the type is an array, the node is an ordered collection of nodes, each conforming to the element type.

Nodes are the products of successful interpretation; the engine passes them around as structured data.

## 1.4 Interpretation Context

An **interpretation context** is created whenever the engine needs to turn a subject into a node. It consists of:

- The **subject** (natural language text).
- An optional **expected type** (if known).
- The annotation of the expected type (system‑provided for `literal` and `array`, user‑defined for `record`).
- A set of **retrieved annotations** (for rules, record types, and actions) that are deemed similar to the current subject/type, obtained through a retrieval mechanism. These annotations are the only information about those definitions that the LLM can see; the full definitions (field lists, rule bodies) are **not** directly visible unless they happen to be retrieved.

If no expected type is supplied, the system must still infer the most appropriate type for the subject, based on the context.

## 1.5 Rule

A **rule** specifies how to interpret a (possibly parameterized) subject into a node of a target type. Rules are the primary mechanism for capturing reusable interpretation patterns.

A rule definition consists of:

- **`id`** – a unique identifier.
- **`annotation`** – a natural‑language description of the rule’s purpose, expected subjects, and result.
- **`for` block** – a (possibly empty) list of formal parameters, each with a name and a type. These parameters represent the variable parts of the subject that are filled in when the rule is invoked. Example: `for: name (literal), age (literal)`.
- **`given` block** – a (possibly empty) ordered list of named intermediate bindings, each of the form `name = value`. These break down the interpretation into smaller steps.
- **`must be` block** – a **value expression** that produces the final node. This expression can use any names bound in `for` or `given`.

When a rule is invoked, the caller provides concrete nodes for every `for` parameter. The engine then evaluates `given` bindings in order, and finally the `must be` expression, yielding the result node.

### Values

Every expression inside a rule (in `given` and `must be`) is a **value**. Values are divided into three classes:

#### Primitive values

A primitive value does not, by itself, open a new interpretation context (except `ref`). It is one of:

- **Var reference** – refers to a name bound in `for` or a previous `given`.  
  *Field access*: if the referenced variable is of a record type, its fields can be projected using dot notation (e.g. `user.name`). This yields the value of that field. Only direct field access is supported; there is no array index access.
- **Interpolated string** – a string that may embed references to other primitive values (e.g. `"Hello, {user.name}"`). Every value has a default string representation; users may customise it through actions (see 1.6), but the mechanism is out of scope. An interpolation argument cannot be a plain static string literal (since literals and strings are the same).
- **`ref` interpretation** – a special form that creates a **new** interpretation context. It supplies a natural‑language subject (written as an interpolated string) and an expected type. The result is the node obtained by fully interpreting that subject under the given expected type.  
  `ref` is the primary tool for modularising interpretation: instead of duplicating complex logic, a rule can delegate a subtask to the engine itself. Example: `ref "a user with name {name} and age {age}" as record user`.
- **Array of primitive values** – a literal array whose elements are primitive values. All elements must conform to the same element type.

#### Dynamic values

A dynamic value combines primitive values to construct a structured node, or invokes a specific rule or action by name. It is one of:

- **Record construction** – specifies a record type (with concrete generic arguments if any) and supplies field values (each a primitive value) for all its fields.
- **Definite rule call** – names a specific rule (by id) and provides primitive values for its `for` parameters. This is a direct, non‑interpretive invocation (the rule body is executed as defined, no LLM search is needed).
- **Action call** – invokes a deterministic, user‑defined action, providing primitive values. The result is computed without LLM involvement.

#### Sequential values

A sequential value wraps a **dynamic value** and causes it to be evaluated element‑wise over arrays, producing an array node. It works by marking one or more array‑valued primitive parameters as `iter`. When evaluated:

- All `iter`‑marked arguments must be arrays of the same length.
- For each index `i`, the dynamic value is evaluated with the `i`‑th element substituted for each `iter` argument; non‑`iter` arguments stay the same.
- The result is an array of the same length, whose elements are the results of each per‑index evaluation.

This is the **only** way to express repetition or element‑wise transformation inside rules. No explicit loops, index variables, or element accessor primitives exist.

## 1.6 Action

An **action** is a deterministic, user‑definable operation that runs outside the LLM‑driven interpretation loop. Actions are meant for closed, well‑defined computations that do not require natural‑language understanding (e.g., string concatenation, arithmetic, conditionals, type‑level checks). An action:

- Has a unique name and a fixed list of parameters.
- Carries an **annotation** that describes its contract (expected input types, output type, behaviour).
- Is implemented in a separate scripting layer (the specification does not fix the implementation language).

Actions are called from within dynamic values. Their role is deliberately limited: the engine’s intelligence should reside in rules and the use of `ref` for sub‑interpretation, not in opaque scripts.

---

# 2. Meta‑Specification: The System Describing Itself

The engine’s own abstract syntax is described using the same kind of definitions. This self‑description serves as the **bootstrap specification**: it allows the engine to interpret the very definitions that constitute it, enabling validation, tooling, and evolution.

## 2.1 Meta‑Types

The following record types are part of the engine’s built‑in knowledge. They define the structure of a specification. All record fields are **mandatory**; the full shape of any instance is given by the combination of these fields and the type’s annotation.

@"A generic tuple of two values"
record Tuple<t1, t2> with val1(t1), val2(t2)

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

@"An action definition. Contains the action's name, annotation, formal parameters, and an output type. The actual implementation is opaque to the engine."
record ActionDef with
  name(literal), annotation(literal),
  params(record Tuple<literal, record TypeSpec>[]),
  outputType(record TypeSpec)

## 2.2 Meta‑Rules

The following rules describe **how to interpret a natural‑language specification text into instances of the above meta‑types**. They constitute the bootstrapping interpreter. Each rule relies on `ref` to delegate parsing subtasks, thereby keeping the system modular and reducing the need for hard‑coded actions. The existence of the `ValueExpr` DSL specification (the annotation of `ValueExpr`) is particularly important: it enables any `ref` call with expected type `ValueExpr` to correctly interpret a natural‑language phrase into a valid DSL string.

### Rule: `interpret-record-def`

@"Interprets a natural language description of a record definition. Returns a RecordDef node. The process extracts the mandatory fields and synthesizes the complete shape annotation, which may incorporate a grammar specification"
rule interpret-record-def for rawText(literal)
given:
  basics: ref(record TypeBasics) "the record name, category, and the raw annotation paragraph contained in the text: {rawText}"
  fieldDecls: ref(record Tuple<literal, literal>[]) "all mandatory field declarations (each with a name and a type description) found in: {rawText}"
  fieldTypes: ref(record TypeSpec[]) "the field definitions with each type fixed as a type spec reference that specifies the type description, transformed from {fieldDecls.val2}"
  constructedFields: seq record Tuple<literal, record TypeSpec> with val1=iter fieldDecls.val1, val2=iter fieldTypes
  canonicalAnnotation: ref(literal) "the complete shape annotation for type {basics.name}, which refines and formalizes the raw annotation '{basics.rawAnnotation}', incorporates the fields {fieldDecls}, and covers any mentioned DSL specification. It is a self-contained description of the type's structure, field contracts, and, if applicable, the DSL grammar and semantics."
must be: 
  record RecordDef with
    id = basics.name, fields = constructedFields, annotation = canonicalAnnotation

*Helper records used implicitly by `ref`:*
- `record TypeBasics` (name: literal, category: literal, rawAnnotation: literal)

### Rule: `interpret-rule-def`

@"Interprets a natural language description of a rule definition. Returns a RuleDef node."
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

*Helper record:*
- `record RuleOutline` (id: literal, annotation: literal)

### Rule: `interpret-ref-call`

@"Interprets a natural‑language description of a ref sub‑interpretation call. The rule produces a ValueExpr node containing the corresponding DSL string 'ref(<type>) \"<subject>\"'. This rule is the dedicated mechanism for handling ref expressions, ensuring that the subject part is always treated as a declarative description, not a command."
rule interpret-ref-call for refDescription(literal)
given:
  subjectPart: ref(literal) "the noun phrase that describes the value to be constructed, as contained in the ref call description: {refDescription}"
  expectedType: ref(literal) "the type name given after 'as' in the ref call description: {refDescription}"
  dsl = ref(literal) "ref({expectedType}) \"{subjectPart}\""
must be: record ValueExpr with dslText = dsl

### Rule: `interpret-value-expr`

@"Given a natural language phrase describing a value expression, produce a ValueExpr node. When the phrase describes a ref sub‑interpretation, the rule delegates to interpret-ref-call to enforce the subject‑style form. Otherwise, it wraps the phrase into a DSL string following the ValueExpr DSL specification."
rule interpret-value-expr for phrase(literal)
given:
  isRefCall: ref(literal) "whether the phrase {phrase} describes a ref sub‑interpretation (i.e., it contains an 'as' clause and the leading part describes a value)"
  refDslText: ref(record ValueExpr) "if {isRefCall} is true, interpret the phrase as a ref call using interpret-ref-call with refDescription: {phrase}; otherwise return an empty ValueExpr (dslText='')"
  defaultDslText: ref(literal) "if {isRefCall} is false, the ValueExpr DSL string that represents the natural language phrase '{phrase}', following the DSL specification"
  dslText: ref(literal) "if {isRefCall} is true, use the dslText from {refDslText}; otherwise use {defaultDslText}"
must be: record ValueExpr with dslText = dslText
