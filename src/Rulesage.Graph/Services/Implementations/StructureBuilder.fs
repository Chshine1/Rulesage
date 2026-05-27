namespace Rulesage.Graph.Services.Implementations

open System
open QuikGraph
open Rulesage.Common.Grammar.Ast
open Rulesage.Graph.Services.Abstractions

type StructureBuilder() =
    interface IStructureBuilder with
        member _.Build rules records actions =
            let structuralGraph = BidirectionalGraph<NodeId, Edge<NodeId>>()
            let mutable nodesMap = Map.empty<NodeId, GraphNode>

            let addNode id desc =
                nodesMap <-
                    nodesMap
                    |> Map.change
                        id
                        (function
                        | None -> Some { Id = id; Description = desc }
                        | some -> some
                        )

                structuralGraph.AddVertex(id) |> ignore

            let rec addTypeExprDeps sourceId typeExpr =
                match typeExpr.Atomic with
                | AtomicType.Record(rid, gs) ->
                    structuralGraph.AddEdge(Edge(NodeId.Record rid, sourceId)) |> ignore
                    gs |> Seq.iter (addTypeExprDeps sourceId)
                | _ -> ()

            let rec addRefDeps source p =
                match p with
                | PrimitiveExpr.Ref r ->
                    let id = NodeId.Ref $"ref_{Guid.NewGuid()}"

                    addNode
                        id
                        (r.Desc
                         |> Seq.choose (
                             function
                             | StringPart.Literal l -> Some l
                             | _ -> None
                         )
                         |> String.concat "")

                    structuralGraph.AddEdge(Edge(id, source)) |> ignore
                    addTypeExprDeps id r.ExpctedType
                | PrimitiveExpr.Array arr -> arr |> Seq.iter (addRefDeps source)
                | _ -> ()

            let addRuleDeps (rule: RuleExpr) =
                let id = NodeId.Rule rule.Id
                addNode id rule.Annotation

                rule.Fors.Values |> Seq.iter (fun v -> addTypeExprDeps id v.Type)

                let processExpr v =
                    match v with
                    | Primitive p -> addRefDeps id p
                    | Dynamic d ->
                        let source, args =
                            match d with
                            | DynamicExpr.Satisfying(ruleId, a) -> NodeId.Rule ruleId, a |> Seq.map _.Value
                            | DynamicExpr.ResultOf(action, a) -> NodeId.Action(fst action), a |> Seq.map _.Value
                            | DynamicExpr.Record(record, a) -> NodeId.Record(fst record), a |> Seq.map _.Value

                        args |> Seq.iter (addRefDeps id)
                        structuralGraph.AddEdge(Edge(source, id)) |> ignore
                    | Seq s ->
                        let source, args =
                            match s with
                            | SeqExpr.Satisfying(ruleId, a) -> NodeId.Rule ruleId, a |> Seq.map _.Value
                            | SeqExpr.ResultOf(action, a) -> NodeId.Action(fst action), a |> Seq.map _.Value
                            | SeqExpr.Record(record, a) -> NodeId.Record(fst record), a |> Seq.map _.Value

                        args |> Seq.iter (addRefDeps id)
                        structuralGraph.AddEdge(Edge(source, id)) |> ignore

                for v in Seq.append (rule.Givens.Values |> Seq.map _.Value) [ rule.MustBe ] do
                    processExpr v

            for r in records do
                let id = NodeId.Record r.Id
                addNode id r.Annotation
                r.Fors.Values |> Seq.iter (fun v -> addTypeExprDeps id v.Type)

            for a in actions do
                let id = NodeId.Action a.Id
                addNode id a.Annotation
                a.Fors.Values |> Seq.iter (fun v -> addTypeExprDeps id v.Type)
                addTypeExprDeps id a.Returns

            rules |> Seq.iter addRuleDeps

            nodesMap, structuralGraph
