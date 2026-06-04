# 1. Meta‑Specification: The System Describing Itself

The system described here is a specification‑only engine. It defines the static shape of types, rules, and values, and
the normative rules for decomposing natural‑language subjects into structured interpretations. The actual execution of
`ref` interpretations and user‑defined actions is delegated to an external, black‑box runtime; this specification does
not prescribe how that runtime works.

It only does "interpretation", and modifications like "replace a rule" are also interpreted, like interpreted to a
standardized modification node.

## Meta‑Ruleset

```text
#common
@"A generic tuple of two values"
record Tuple<t1, t2> with val1(t1), val2(t2)

#common
@"Group an array of objects by equivalance of a given key, returns the dictionary of groups"
action groupBy<t> on array(t[]), groupKey(literal) returns record Tuple<literal, t[]>[]

#common
@"Find a dictionary value of the given key"
action findInDictionary<tk, tv> on dictionary(record Tuple<tk, tv>[]), key(tk) returns tv
```

```text
#meta-type
@"A type spec reference. It only has a literal string *typeId*, which must be of `type-id := base-id (\"[]\")?`, and `base-id = \"literal\" | (\"record\" record-id generic-params) | type-id`. Here generic params are passed as `\"<\"t1,t2,...\">\"` if needed, and `t1,t2` are again type identifiers or generic names provided a generic context."
record TypeSpec with typeId(literal)

#meta-type
@"Interprets a natural language type description phrase into a TypeSpec, where the type can only be a literal string/generic record and their arrays without any other type features, provided a generic context (a name‑description dictionary of generic type parameters)."
rule interpret-type-spec for typeDesc(literal), genericContext(record Tuple<literal, literal>[])
given:
  availableUnits: result of retrieveSimilarUnits where query = $for.typeDesc
  groupedUnits: result of groupBy<record UnitSummary> where array = $given.availableUnits, groupKey = "unitType"
  availableRecords: result of findInDictionary<literal, record UnitSummary[]> where dictionary = $given.groupedUnits, key = "record"
  rawType:
    ref(record Tuple<literal, record Tuple<literal, literal>[]>) in none
        "a pair for the type description \"{$for.typeDesc}\", using available records {$given.availableRecords} and generic context {$for.genericContext}."
       +"The first element is a type expression string that may contain array suffixes \"[]\" (allow multiple), and may contain:"
       +" - \"literal\""
       +" - generic parameter names from the keys of generic context"
       +" - record ids from availableRecords (which may include generic placeholders)"
       +"For generic record ids, they don't have to be closed, allowing freshly introduced open placeholder names (but must NOT collide with keys in generic context)."
       +"The second element is a dictionary of (placeholder name, needed type description) for each open placeholder that still needs to be resolved from its description into an actual TypeSpec."
  genericParams:
    ref(record Tuple<literal, record TypeSpec>[]) in meta-type
        "a dictionary of name-TypeSpec resolved from (name, needed type descriptions): {$given.rawType}, with generic context: {$for.genericContext}."
must be:
  ref(TypeSpec) in none
      "the final, fully closed TypeSpec from a raw type {$given.rawType}, with open generic placeholders resolved by types: {$given.genericParams}."
```

### 1. Records

```text
#meta-record
@"A record definition. It has a unique identifier *id*, an array *fields* of the full set of mandatory fields (a key-type typle for each), and an annotation that is the complete structural contract of the type (including any grammar syntax when applicable)."
record RecordDef with
  annotation(literal), id(literal), fields(record Tuple<literal, record TypeSpec>[])

#none
@"A record definition intent with a string name, a name-description dictionary of generic parameters, and a (name, expected type definition) dictionary of fields."
record RecordDefIntent with
  name(literal), genericParams(record Tuple<literal, literal>[]), fields(record Tuple<literal, literal>[])

#meta-record
@"Interprets a natural-language text that describes exactly one record type definition into a standardized RecordDef"
+"The description must declare all mandatory fields with their names and a detailed type and structural description. And it should be purely structural and declarative: describing what shape the data has, not how it is computed or when it is used"
rule interpret-record-def for rawText(literal)
given:
  intent:
    ref(record RecordDefIntent) in none
        "the intent for the record to be defined in: {$for.rawText}, where:"
       +"*name* is the name for the record in PascalCase, "
       +"*genericParams* is a name-description dictionary of generic type parameters, "
       +"*fields* is a (name, expected type description) dictionary. These types can refer to the generic parameters"
  fieldTypes:
    ref(record TypeSpec[]) in meta-type
      "explicit type reference specs for the descriptions {$given.intent.fields.val2}, provided a generic context {$given.intent.genericParams}"
  fields:
    seq record Tuple<literal, record TypeSpec> with
      val1 = iter $given.intent.fields.val1,
      val2 = iter $given.fieldTypes
  recordId:
    ref(literal) in none
      "the full id in the form of `name(\"<\"name1, name2, ...\">\")` where namei are generic param names, givem name: {$given.intent.name} and params: {$given.intent.genericParams.val1}"
  annotation:
    ref(literal) in meta-record
        "the complete declarative description of the structure and shapes for the record definition: {$for.rawText}, whose name is {$given.intent.name}, has generic parameters: {$given.intent.genericParams} and fields: {$given.intent.fields}"
must be:
  record RecordDef with
    id = $given.recordId,
    fields = $given.constructedFields,
    annotation = $given.annotation
```

### 2. Rules

```text
#meta-rule
@"A rule definition. Contains an id, an annotation, its formal parameters (for-block), local bindings (given-block), and the final value expression (must-be)."
record RuleDef with
  id(literal), annotation(literal),
  forParams(record Tuple<literal, record TypeSpec>[]),
  givenBindings(record Tuple<literal, record ValueExpr>[]),  
  mustBe(record ValueExpr)

#meta-rule
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
#meta-rule
@"Interprets a natural-language text that describes exactly one rule definition into a RuleDef."
+"\nIt must be declarative and interprative: it states what will be interpreted to what a standardized form, not the procedure to execute something."
+"It should describe a subject to be interpreted, which may be parametrized among a whole subject type, then steps of intermediate interpretations/actions/record constructions. They should finally build to a standardized instance, which is \"what this subject will be interpreted to\"."
+"The constraint can be put on the subject for the interpretation to run at better performance."
+"The description shouldn't define new record shapes/actions inline. If also shouldn't just be a trivial extraction like output merely copies an input field; The interpretation must add structural/domain constraints, etc."
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

### 3. Actions

```text
#meta-action
@"An action definition. Contains the action's name, annotation, formal parameters, output type, and an actual Lua script implementation of the action."
record ActionDef with
  name(literal), annotation(literal),
  params(record Tuple<literal, record TypeSpec>[]),
  outputType(record TypeSpec),
  luaScript(literal)

#meta-action
@"Interprets a natural-language text that describes exactly one action definition into an ActionDef."
+"The description must describe an \"action\", which is to be applied to a set of typed parameters."
+"Then describe a domain-indepentent, atomic operation on these params which does not contain any semantic logic, any rule/action call, and isn't a simple action composable from field accessing/record construction. This needs to produce a final result."
+"It shouldn't describe a record shape or a behavioural rule, nor do rule‑specific interpretations."
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

```shell
dotnet run ruleset init
dotnet run ruleset save --input ./Resources/Rules/meta-rules.rsg
dotnet run ruleset truncate
dotnet run network visualize --input ./Resources/Rules/meta-rules.rsg --output ./Resources/Rules/dot
dotnet run network discover --input ./Resources/Rules/meta-rules.rsg --output ./Resources/Rules/dot --label "interpret-project-items:pi" "interpret-type-spec:ts" "interpret-rule-def:rd" "interpret-record-def:rcd" "interpret-action-def:ad"
```

We are building a "ruleset", which is a finite set of records, actions and rules.

The final mission for the ruleset is to "interpret" a "subject" into some records.

"Add a command which solely generates embeddings for all database entities' annotation field"

Sure just adding a command? Is there something that already exists? I'll stop this and report back on a reason "There is
a command which already handles this"

- Meaning: When we are adding something, we first need to determine if it exists, and stops, reports if it indeed exists
- **Rephrase**: Adding (something) = (If this #something exists, and give a #reason) + action reports: "This something
  already exists (#reason)" if #exists, otherwise add (something), knowing that it doesn't exist
- **Suggests**: When we are adding something, we want to know if it exists. If it exists already, we can directly
  translate this add process, otherwise check first.
  Commands are organized as root-subcommands -> many subcommands
- **Rephrase**: A cli project has commands, which handle a set of functionalities, distributed to commands
- **Suggests**: Project (architecture: cli, csharp + fsharp, functionality: AI coding **NO, They are actual
  implementations**); Command (functionality); Organization; (models, should come with annotations, but they can't find
  actual structures)
- A cli program is an object where you can input something, then trigger action based on the input, stateless once an
  action is complete
- The program may handle several responsibility/actions. However, it may just handle one action, just one script which
  runs something
- "We are using sevaral responsibility" is an instance. But "We are using a cli project" is also an instance?
  I see that there are commands on network, ruleset and a common command. Network does network analysis on an existing
  ruleset, Ruleset operates on rulest itself, and Common do other things
  The rulset is exactly stored in the database, or when we are talking about database, we are just talking about the
  ruleset
  So I look under Ruleset command (and if we are to add, add here), there are init, save search, truncate
  (Actually I think when I'm looking at init, save, search, truncate, I don't know what they are exactly, I need further
  reminders, the same for root commands)
  Init applies migrations, only sets up the database structure, isn't related.
  Save reads a given dsl file and parses it, saves to database, no. But looks like it handles db updates, so maybe it
  has behaviours related to embedding generations:
  Search is pure queries, no
  Truncate clears out the database, no
  So the answer is, no, I can indeed add a command, from discussion above, I know that we should add a subcommand under
  Ruleset

What is "add"? Actually it's just add, but we cannot standardly add many things, we can only standardly add projects,
directories, files and file contents.
Meaning that we have things that "standard addition is capable of". If adding a thing that's not these, then it should
be split into these
But no, it's not actually **just** add. Oh, no we can *add to rulesets*.

We can only standardly add projects, directories, files, file contents or a rule, or rule content.
What's the difference of adding content or modifying content?

Or we say: Adding something to something, adding something is adding something to the solution (implicitly)
So adding something to something, meaning that latter "something" consists of lots of things and we are adding: say
The solution consists of a ruleset, projects, solution folders and isolated files

Adding something to the solution means: What will this something be? No

How to add a sub-command exactly?

I know that adding a sub-command is to create a file named `<Command>Command.cs` under the corresponding root-subcommand
directory, it could give you the ability to create a command instance in Program.cs and registers it
Then create the command instance in Program, registers it.
In `<Command>Command.cs`, there will be a static partial class named as <RootCommand>Commands, with only one public
method named as Create<Command>Command, accepting an IServiceProvider which is the DI container it can resolve services
from, and returns a required Command instance.
Then "create the command instance in Program, registers it" steps knows the class has this signature, calls it. It also
knows that the Main function has sevaral parts: Building IHost, completing RootCommand, execute, this will be added to "
completing RootCommand", and uses the IServiceProvider from the built IHost.

Or: The project entrypoint is the Main function, we want it to be a cli project with several commands, each with a
function

The choice is that we are using System.CommandLine, which needs a RootCommand and register subcommands, then execute.
It's implemented by the program completing RootCommand + execute section.

Each command handles a functionality, which may come with parameters. They are implemented by setting command options,
then call SetAction.

Because every async function needs a CancellationToken.

SetAction is a function call, then it must accept an instance of the required type. Also, it's something which will be
called which can handle the required functionality.

It accepts Func<ParseResult,CancellationToken,Task>, an instance here should directly be an async lambda with parameters
async (result, cancellationToken) => { The async body that handles the functionality }

We are using DI, and we use "handlers" to wrap command actions, each root command corresponds to a handler class
resolved from DI, with methods corresponding to subcommands, implementing their functionalities.

Thus the async lambda body is simply creating a scope, resolve the handler from the scope, read the parameters from
result, and pass them to the handler method to call it

But after that, the handler can do whatever it wants.

Now async lambda needs a service provider to create the scope and use DI. So we are using a factory method for each
command: It accepts an IServiceProvider, creates the command with options, SetAction which uses this service provider
and finally returns it.

But for the command to work, we call this factory method to create the command in Program, then use Subcommands.Add to
do it.

It needs an IServiceProvider, Program starts with building an IHost and configuring its services, served as the host for
the program, use its service provider.

**INSIGHT**: Everything comes with a "reason"?

Or say, we cannot state "a subcommand is a rootcommand's sub commands with something?" No we can't

A "mind model" on "objects" for the project: Command, Parameters, Services, etc. They are all models.
Method Call, Method Parameter, Whatever (Makes sense)

So we are actually building **Domain concepts** (previous "subjects"). And we don't "interpret" them, but **implement
them**
For example, a **Command** is a domain concept, it's implemented by a **root command**, a **subcommand** and a **command
action** (all domain concepts)
(So what do we mean by "implementing" here?)
**INSIGHT**: We say that the "domain concept" command is an ability to identify a user input, read user parameters and
delegate it to the intended action.
Then it's "implemented", because root command + subcommand identifies it, subcommand can read user parameters, and there
is an action.
**MAKES SENSE**

## Formal

ruleset meta:
1. concept: a concept has a *name* and an *annotation*, both literals, and a name-type dictionary of *fields*, which (define the typed attributes that instances of the concept must hold).
2. transformation: a transformation has *name* and *annotation* literals, a key-type dictionary of *sources*, a *result* type and a literal Lua *script*, describing an atomic & non-semantic deterministic transformation from the sources to a result instance.
3. rule: a rule has *name* and *annotation* literals, a key-type dictionary of *coreConcepts*, an array of *implementationSteps*, describing how to implement the core concepts according to the project standard.
4. implementation-step: an implementation step contains only a literal *dslExpression*, which (is a string in a project-specific domain-specific language that captures one imperative step of the implementation).
5. unit: a ruleset unit consists of only a literal *reference*, referring to either one concept, transformation or rule.
6. domain: a domain consists of a collection of *units*, which (are ordered and together describe a self-contained concern within the ruleset).
7. ruleset: a ruleset has a set of *concepts* and *rules*, organized by *domains*, which fully describe a set of project standards, and the project status/choices subordinate to the standards.

cli program:
1. stdin: the standard input stream, a singleton readable byte stream provided by the operating system.
2. stdout: the standard output stream, a singleton writable byte stream provided by the operating system.
3. stderr: the standard error stream, a singleton writable byte stream provided by the operating system for diagnostic messages.
4. program: a CLI program has a literal *name*, it implicitly owns the three standard streams stdin, stdout and stderr; it represents the executable entry point of the application.

structural parsing: an implementation form for the *cli program*
1. command: a command has a literal *name*, an optional literal *annotation*, a list of *subcommands*, and a list of *options*; it models a verb or action the program can perform.
2. subcommand: a subcommand is a command nested inside another command, inheriting its structure and allowing hierarchical verb organisation.
3. option: an option has a literal *name*, an optional literal *alias*, a *type*, and a literal *description*; it represents a named parameter that modifies the behaviour of a command.
4. user-input: 

.NET engineering:
1. solution: a solution has a *name*, a *root directory*, and contains *solution folders* and *projects*.
2. solution folder: a solution folder has a *name* and contains *solution folders* and *projects*; it provides logical organisation within a solution.
3. project: a project has a *name*, a *language*, a *target framework*, and contains *source files*, *project references*, *package references*, and *assembly references*.
4. project reference: a project reference refers to another *project* within the same *solution* by its *name* or *path*.
5. package reference: a package reference refers to a *NuGet package* by *name* and *version*.
6. assembly reference: an assembly reference refers to a compiled *assembly* by its *file path* or *strong name*.
7. NuGet package: a NuGet package has a *name*, a *version*, and contains *assemblies*, *content files*, and *dependency* packages.

CLR:
1. assembly: an assembly has a *name*, a *version*, a *culture* (optional), a *public key token* (optional), and contains *modules*, *types*, and *manifest* metadata.
2. module: a module contains *types* and *IL code*; an assembly can be composed of one or more modules.
3. type: a type has a *name*, a *namespace*, a *visibility*, and defines *members*; it can be a class, struct, interface, enum, or delegate.
4. member: a member has a *name* and a *member type* (field, method, property, event, constructor).
5. method: a method has a *name*, *parameter types*, a *return type*, and an *IL body* (if not abstract/external).
6. field: a field has a *name*, a *type*, and optional *literal value*.
7. property: a property has a *name*, a *type*, and *get/set accessor methods*.
8. event: an event has a *name*, a *delegate type*, and *add/remove accessor methods*.
9. attribute: an attribute has a *type* and *constructor arguments*; it can be attached to *assemblies*, *types*, or *members*.
10. metadata: metadata tables describe *types* and *members* in a binary format understood by the runtime.
11. IL: IL (Intermediate Language) is the CPU-independent instruction set to which .NET languages compile.
12. AppDomain: an AppDomain is an isolation boundary for *assemblies* and *security* within a process (primarily in .NET Framework; in modern .NET, assembly load context replaces it).
13. assembly load context: an assembly load context controls *assembly* loading, isolation, and sharing in modern .NET.
14. garbage collector (GC): the GC manages automatic memory for *managed objects* by *generations*.
15. managed object: a managed object is an instance of a *type* allocated on the *managed heap* and tracked by the *GC*.
16. value type: a value type has *struct* layout and its instances are stored inline (stack or containing object).
17. reference type: a reference type has *class* layout and its instances are allocated on the *managed heap*.

C#:
1. class: a class is a *reference type* that may contain *fields*, *methods*, *properties*, etc.
2. struct: a struct is a *value type* that may contain *fields*, *methods*, *properties*, etc.
3. interface: an interface defines a contract of *methods*, *properties*, *events*, without implementation.
4. enum: an enum is a named set of integral *constants*.
5. delegate: a delegate is a type-safe function pointer referencing one or more *methods*.
6. record: a record is a *reference type* (or *value type* for `record struct`) with value-based equality and immutable properties by default.
7. namespace: a namespace organizes *types* and prevents naming collisions.
8. using directive: a using directive imports *namespaces* or creates aliases.
9. generic type parameter: a generic type parameter has a *name* and optional *constraints*.
10. async method: an async method uses `async`/`await` and returns `Task`, `Task<T>`, or `ValueTask<T>`.
11. LINQ: Language Integrated Query provides query expressions over *IEnumerable<T>* or *IQueryable<T>*.

F#:
1. module: a module groups *values*, *types*, and *functions*; it is the primary compilation unit.
2. discriminated union: a discriminated union defines a type that can be one of several named *cases*, each with optional payload data.
3. record (F#): an F# record is an immutable named product type with structural equality.
4. type abbreviation: a type abbreviation creates an alias for an existing *type*.
5. function: an F# function has a *name* (optional), *parameters*, and a *body expression*; functions are first-class values.
6. computation expression: a computation expression provides a syntax for monadic/effectful computations (e.g., `async { }`, `task { }`).
7. pattern matching: pattern matching deconstructs *values* against *patterns* such as *discriminated union cases*, *tuples*, *records*, etc.
8. active pattern: an active pattern provides a named *pattern* for use in pattern matching, abstracting away data representation.
9. unit: `unit` is a singleton type representing the absence of a meaningful value.

Hosting (.NET Generic Host):
1. host: a host encapsulates an application's *DI container*, *configuration*, *logging*, and *lifetime*.
2. host builder: a host builder configures and creates a *host* via a fluent API.
3. hosted service: a hosted service is a long-running *background task* managed by the *host* lifetime.
4. application lifetime: application lifetime provides *start*, *stop*, and *shutdown* hooks for the *host*.
5. host environment: the host environment provides *environment name*, *content root path*, and *application name*.

Configuration:
1. configuration source: a configuration source provides key-value pairs from a medium (JSON file, environment variable, command line, etc.).
2. configuration provider: a configuration provider reads from a specific *configuration source* and populates a *configuration* tree.
3. configuration root: the configuration root is the merged view of all *configuration providers*.
4. configuration section: a configuration section is a sub-tree of the configuration data, identified by a *path*.
5. options pattern: the options pattern binds *configuration sections* to strongly typed *options classes* via *options configuration*.
6. options class: an options class is a plain .NET *type* that holds configuration values.
7. options configuration: options configuration maps *configuration sections* to *options classes* and can apply post-configure actions.

Dependency Injection (DI):
1. service collection: a service collection holds a set of *service descriptors*.
2. service provider: a service provider resolves services based on registered *service descriptors*.
3. service descriptor: a service descriptor maps a *service type* to an *implementation type* or *instance*, with a *lifetime*.
4. service lifetime: service lifetime can be *singleton*, *scoped*, or *transient*.
5. singleton: one instance shared across the entire *service provider*.
6. scoped: one instance shared within a *scope* (e.g., per web request).
7. transient: a new instance is created each time the service is requested.
8. service scope: a service scope is a logical boundary that controls the lifetime of *scoped* services.

Persistence:
1. entity: an entity has a unique *identity* and a set of *properties*; it is a domain object with a lifecycle.
2. value object: a value object has no identity; it is defined by its *structural equality* of its *attributes*.
3. aggregate root: an aggregate root is an *entity* that acts as the entry point to a cluster of *entities* and *value objects* (an aggregate).
4. repository: a repository mediates between *entities* and the data store, providing collection-like access.
5. unit of work: a unit of work tracks changes to *entities* and coordinates writing them to the data store as a single transaction.
6. data context: a data context (e.g., `DbContext`) represents a session with the database and manages *entity* loading, change tracking, and persistence.
7. migration: a migration captures incremental schema changes to bring the database to the desired *model* state.

PostgreSQL:
1. connection: a connection represents a TCP session to a *PostgreSQL server* with *authentication parameters*.
2. command: a command represents a SQL statement executed against a *connection*, with *parameters* and *result sets*.
3. transaction: a transaction groups *commands* into an atomic unit of work with ACID guarantees.
4. schema: a schema is a namespace that contains *tables*, *functions*, *types*, etc.
5. table: a table has a *name* and a set of *columns*, each with a *data type* and constraints.
6. index: an index speeds up data retrieval on one or more *columns*; types include B-tree, Hash, GiST, GIN, etc.
7. function: a PostgreSQL function is a server-side routine written in SQL, PL/pgSQL, or other procedural languages.
8. extension: an extension adds optional features (e.g., `pgvector`, `postgis`).

Npgsql:
1. NpgsqlConnection: represents an open *connection* to a PostgreSQL database; derived from `DbConnection`.
2. NpgsqlCommand: represents a *command* to execute against an *NpgsqlConnection*; supports named *parameters*.
3. NpgsqlDataReader: forward-only, read-only stream of *rows* from a query.
4. NpgsqlParameter: a named parameter for an *NpgsqlCommand*, with a *data type* and *value*.
5. NpgsqlTransaction: local or distributed *transaction* object obtained from an *NpgsqlConnection*.
6. NpgsqlBatch: enables batching of multiple SQL statements in a single round trip.

Vector Persistence (pgvector):
1. vector: a vector is an array of floating-point numbers representing an *embedding*.
2. embedding: an embedding is a numeric representation of data (text, image, etc.) produced by an *embedding model*.
3. vector table: a table that includes a *vector column* to store embeddings alongside other relational data.
4. vector index: an index on a *vector column* for approximate nearest neighbour search (e.g., *IVFFlat*, *HNSW*).
5. distance function: a function that computes similarity between vectors (L2 distance, inner product, cosine distance).
6. approximate nearest neighbour (ANN) search: a query that retrieves vectors most similar to a given query vector, using a *vector index* for speed.
