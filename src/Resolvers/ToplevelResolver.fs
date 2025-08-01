(*
 * Copyright (C) 2025 TricolorHen061 - tricolorhen061@duck.com
 *
 * Licensed under the GNU General Public License version 3 (GPLv3)
 * See LICENSE file for details.
*)


module Resolvers.ToplevelResolver

open Types
open Types.Base
open Resolvers.BaseResolvers
open Resolvers.ScopeResolvers
open Utils
open FsToolkit.ErrorHandling
open Misc

let rec tryResolveStructDeclaration
    (astData: Ast.ASTData)
    ((exportedStatus, structName, generics, structConstruct): Ast.StructData)
    =
    result {
        let! resolvedGenerics = tryResolveGenerics astData generics
        let! resolvedFields, resolvedStructs, resolvedInterfaces = tryResolveStructConstructs astData generics structConstruct

        

        return
            (({ ExportedStatus = exportedStatus
                Name = structName
                Generics = resolvedGenerics
                ActualFields = resolvedFields
                EmbeddedStructs = resolvedStructs
                EmbeddedInterfaces = resolvedInterfaces
                    })
            : ResolvedAst.StructDeclarationData)

    }

and tryResolveStructConstructs (astData: Ast.ASTData) (generics:Generics) (constructs: list<Ast.StructConstruct>) =
    result {
        let mutable (fields: ResolvedAst.StructFieldDeclarationData list) = []
        let mutable (embeddedStructs: ResolvedAst.StructReference list) = []
        let mutable (embeddedInterfaces: ResolvedAst.InterfaceReference list) = []

        for construct in constructs do
            do!
                match construct with
                | Ast.StructConstruct.Field(exportedStatus, (unresolvedType, fieldName), tag') ->
                    tryResolveType astData generics unresolvedType
                    |> Result.map (fun result ->
                        fields <-
                            Misc.listAdd
                                fields
                                (({ ExportedStatus = exportedStatus
                                    Type = result
                                    Name = fieldName
                                    Tag = tag' })
                                : ResolvedAst.StructFieldDeclarationData))
                | Ast.StructConstruct.Embedded(AstStruct astData structDecData,
                                                                      genericsInstanData) ->
                    result {
                        let! resolvedGenericsInstaniation = tryResolveGenericsInstantiation astData generics genericsInstanData
                        let! resolvedStruct = tryResolveStructDeclaration astData structDecData

                        embeddedStructs <-
                            Misc.listAdd
                                embeddedStructs
                                (({ StructDeclaration = resolvedStruct
                                    GenericsInstantiation = resolvedGenericsInstaniation }))
                    }
                | Ast.StructConstruct.Embedded(AstInterface astData interfaceDecData,
                                                                      genericsInstanData) ->
                    result {
                        let! resolvedGenericsInstaniation = tryResolveGenericsInstantiation astData generics genericsInstanData
                        let! resolvedInterface = tryResolveInterface astData interfaceDecData

                        embeddedInterfaces <-
                            Misc.listAdd
                                embeddedInterfaces
                                (({ InterfaceDeclaration = resolvedInterface
                                    GenericsInstantiation = resolvedGenericsInstaniation }))
                    }
                | Ast.StructConstruct.Embedded(typeName, _) ->
                    Error [ $"Type with name '{typeName}' is not a struct or interface" ]

        return fields, embeddedStructs, embeddedInterfaces




    }


and tryResolveInterface
    (astData: Ast.ASTData)
    ((exportedStatus, interfaceName, generics, interfaceConstructs): Ast.InterfaceData)
    : Result<ResolvedAst.InterfaceDeclarationData, string list> =
    result {
        let! resolvedGenerics = tryResolveGenerics astData generics

        let! resolvedMethodDeclarations, resolvedStructs, resolvedInterfaces, resolvedTypeSets =
            tryResolveInterfaceConstructs astData generics interfaceConstructs

        return
            (({ Name = interfaceName
                Generics = resolvedGenerics
                ExportedStatus = exportedStatus
                EmbeddedInterfaces = resolvedInterfaces
                ActualMethods = resolvedMethodDeclarations
                EmbeddedStructs = resolvedStructs
                TypeSets = resolvedTypeSets})
            : ResolvedAst.InterfaceDeclarationData)
    }


and tryResolveInterfaceConstructs (astData: Ast.ASTData) (generics:Generics) (constructs: Ast.InterfaceConstruct list) =
    result {
        let mutable (methods: ResolvedAst.MethodFieldData list) = []
        let mutable (embeddedStructs: ResolvedAst.StructReference list) = []
        let mutable (embeddedInterfaces: ResolvedAst.InterfaceReference list) = []
        let mutable (embeddedTypeSets: ResolvedAst.TypeSetData list) = []

        for construct in constructs do
            do!
                match construct with
                | Ast.InterfaceConstruct.TypeSet(types) -> result {
                    let! resolvedTypes = 
                        types
                        |> List.map (fun (includeUnderlyingType, t) -> result {
                            let! resolvedType = tryResolveType astData generics t
                            return {|IncludeUnderlyingType = includeUnderlyingType; Type = resolvedType|}
                        })
                        |> List.sequenceResultA
                        |> Result.mapError Misc.flattenList

                    embeddedTypeSets <- Misc.listAdd embeddedTypeSets {
                        Types = resolvedTypes
                    } 
                    }
                | Ast.InterfaceConstruct.Method(exportedStatus, methodName, parameters, returnType) ->
                    result {
                        let! resolvedParameters =
                            parameters
                            |> List.map (tryResolveParameter astData generics)
                            |> List.sequenceResultA
                            |> Result.mapError Misc.flattenList

                        let! resolvedReturnType = tryResolveType astData generics returnType

                        methods <-
                            Misc.listAdd
                                methods
                                (({ ExportedStatus = exportedStatus
                                    Name = methodName
                                    Parameters = resolvedParameters
                                    ReturnType = resolvedReturnType })
                                : ResolvedAst.MethodFieldData)
                    }

                | Ast.InterfaceConstruct.Embedded(AstStruct astData structDecData,
                                                                            genericsInstanData) ->
                    result {
                        let! resolvedGenericsInstaniation = tryResolveGenericsInstantiation astData generics genericsInstanData
                        let! resolvedStruct = tryResolveStructDeclaration astData structDecData

                        embeddedStructs <-
                            Misc.listAdd
                                embeddedStructs
                                (({ StructDeclaration = resolvedStruct
                                    GenericsInstantiation = resolvedGenericsInstaniation }))
                    }
                | Ast.InterfaceConstruct.Embedded(AstInterface astData interfaceDecData,
                                                                            genericsInstanData) ->
                    result {
                        let! resolvedGenericsInstaniation = tryResolveGenericsInstantiation astData generics genericsInstanData
                        let! resolvedInterface = tryResolveInterface astData interfaceDecData

                        embeddedInterfaces <-
                            Misc.listAdd
                                embeddedInterfaces
                                (({ InterfaceDeclaration = resolvedInterface
                                    GenericsInstantiation = resolvedGenericsInstaniation }))
                    }
                | Ast.InterfaceConstruct.Embedded(typeName, _) ->
                    Error [ $"Type with name '{typeName}' is not a struct or interface" ]

        return methods, embeddedStructs, embeddedInterfaces, embeddedTypeSets

    }

let tryResolveAlias (astData:Ast.ASTData) ((exportedStatus, aliasName, generics, t):Ast.AliasData): Result<ResolvedAst.AliasDeclarationData, string list> = 
    result {
        let! resolvedType = tryResolveType astData generics t
        let! resolvedGenerics = tryResolveGenerics astData generics
        return
         ({ ExportedStatus = exportedStatus
            Name = aliasName
            Type = resolvedType
            Generics = resolvedGenerics })
    }

let tryResolveEnum (astData:Ast.ASTData) ((name, cases):Ast.EnumData) = result {
    let! mappedCases = 
        cases
        |> List.map (fun (caseName, fields, id) -> result {
            let! resolvedFields = 
                fields
                |> List.map(fun (fieldName, fieldType) -> result {
                    // come back to implement generics
                    let! resolvedType = tryResolveType astData [] fieldType
                    return {|Name = fieldName; Type = resolvedType|}
                })
                |> List.sequenceResultA
                |> Result.mapError Misc.flattenList
            return 
                (({
                    Name = caseName
                    Fields = resolvedFields
                    Id = id
                }):ResolvedAst.EnumCase)
        }
            
        )
        |> List.sequenceResultA
        |> Result.mapError Misc.flattenList
    return ({Name = name; Cases = mappedCases}:ResolvedAst.EnumDeclarationData)

}

let tryResolveWrapperType (astData:Ast.ASTData) ((exportedStatus, wrapperTypeName, generics, unresolvedType):Ast.WrapperTypeData): Result<ResolvedAst.WrapperTypeDeclarationData, string list> =
    result {
        let! resolvedGenerics = 
            generics
            |> List.map (tryResolveGenericData astData) 
            |> List.sequenceResultA
            |> Result.mapError Misc.flattenList
        
        let! resolvedType = tryResolveType astData generics unresolvedType
        
        return
            {   ExportedStatus = exportedStatus
                Name = wrapperTypeName
                Generics = resolvedGenerics
                Type = resolvedType }
    }

let tryResolveMethodWithoutScope (astData:Ast.ASTData) (resolvedStructs:Map<string, ResolvedAst.StructDeclarationData>) (resolvedWrapperTypes:Map<string, ResolvedAst.WrapperTypeDeclarationData>) ((exportedStatus, methodName, (((typeName, genericsInstantiationData), receiverParameterName), parameters), returnType, _):Ast.MethodData) = 
    result {
        let! resolvedParameters =
                parameters
                |> List.map (resolveParameter astData [])
                |> List.sequenceResultA
                |> Result.mapError Misc.flattenList

        let! resolvedReturnType = tryResolveType astData [] returnType
        let! resolvedGenericsInstaniationData = tryResolveGenericsInstantiation astData [] genericsInstantiationData
        let! resolvedReceiverType =
            resolvedStructs
            |> Map.tryFind(typeName)
            |> Option.map (fun x -> ResolvedAst.MethodReceiverType.StructReceiver (x, resolvedGenericsInstaniationData))
            |> Option.orElse (Map.tryFind typeName resolvedWrapperTypes |> Option.map(fun x -> ResolvedAst.MethodReceiverType.WrapperTypeReceiver(x, resolvedGenericsInstaniationData)))
            |> fromOptionToResult $"Receiver type (of method '{methodName}') must be either a wrapper type or struct type"
            |> Result.mapError (fun x -> [x])

        return (({
            ExportedStatus = exportedStatus
            Name = methodName
            ReceiverType = resolvedReceiverType
            Parameters = resolvedParameters
            ReturnType = resolvedReturnType
            ReceiverParameterName = receiverParameterName
        }):ResolvedAst.MethodDeclarationDataWithoutScopeData)
    }

let rec tryResolveConst (astData: Ast.ASTData) ((exportStatus, identifierName, constExpression): Ast.ConstData) =
    result {
        let! resolvedConstExpression = toResolvedConstExpression astData constExpression

        return
            (({ ExportedStatus = exportStatus
                IdentifierName = identifierName
                Expression = resolvedConstExpression })
            : ResolvedAst.ConstDeclarationData)

    }

and toResolvedConstExpression
    (astData: Ast.ASTData)
    (input: Ast.ConstExpression)
    : Result<ResolvedAst.ConstExpression, string list> =
    match input with
    | Ast.ConstExpression.Boolean(b) -> ResolvedAst.ConstExpression.Boolean({ Value = b }) |> Ok
    | Ast.ConstExpression.Float(isNeg, f, isImaginary) ->
        ResolvedAst.ConstExpression.Float(
            { IsNegative = isNeg
              Float = f
              IsImaginary = isImaginary }
        )
        |> Ok
    //| Ast.ConstExpression.Iota -> Ok <| ResolvedAst.ConstExpression.Iota
    | Ast.ConstExpression.Identifier(identifierName) ->
        result {
            let! data =
                astData.Consts
                |> Map.tryFind (identifierName)
                |> Misc.fromOptionToResult [ $"Const '{identifierName}' does not exist" ]

            let! resolved = tryResolveConst astData data
            return ResolvedAst.ConstExpression.Const(resolved)
        }

    | Ast.ConstExpression.String(str) -> ResolvedAst.ConstExpression.String(str) |> Ok
    | Ast.ConstExpression.Number(isNegative, str, numberProperty) ->
        ResolvedAst.ConstExpression.Number(
            { IsNegative = isNegative
              StringRepresentation = str
              Property = numberProperty }
        )
        |> Ok

let tryResolveFunctionDeclarationWithoutScope (astData:Ast.ASTData) ((exportStatus, functionName, generics, parameters, returnType, (_, scopeId)):Ast.FunctionData): Result<ResolvedAst.FunctionDeclarationDataWithoutScope, string list> =
    result {
            let! resolvedParameters =
                parameters
                |> List.map (resolveParameter astData generics)
                |> List.sequenceResultA
                |> Result.mapError Misc.flattenList

            let! resolvedGenerics = tryResolveGenerics astData generics
            let! resolvedReturnType = tryResolveType astData generics returnType


            return
                {   ExportedStatus = exportStatus
                    Name = functionName
                    Generics = resolvedGenerics
                    Parameters = resolvedParameters
                    ReturnType = resolvedReturnType }
            
        }


let tryResolveDeclarations (astData:Ast.ASTData): Result<ResolvedAst.ResolvedDeclarations, string list> = 
    result {
        let getValues (m:Map<'K, 'V>) = m |> Map.toList |> List.map snd
        
        let! resolvedStructs =
            astData.Structs
            |> getValues
            |> List.map (tryResolveStructDeclaration astData)
            |> List.sequenceResultA
            |> Result.mapError Misc.flattenList

        let! resolvedEnums = 
            astData.Enums
            |> getValues
            |> List.map (tryResolveEnum astData)
            |> List.sequenceResultA
            |> Result.mapError Misc.flattenList

        // here bro

        let! resolvedInterfaces = 
            astData.Interfaces
            |> getValues
            |> List.map (tryResolveInterface astData)
            |> List.sequenceResultA
            |> Result.mapError Misc.flattenList
        
        let! resolvedAliases = 
            astData.Aliases
            |> getValues
            |> List.map (tryResolveAlias astData)
            |> List.sequenceResultA
            |> Result.mapError Misc.flattenList

        let! resolvedWrapperTypes =
            astData.WrapperTypes
            |> getValues
            |> List.map (tryResolveWrapperType astData)
            |> List.sequenceResultA
            |> Result.mapError Misc.flattenList

        let! resolvedFunctions = 
            astData.Functions
            |> getValues
            |> List.map (tryResolveFunctionDeclarationWithoutScope astData)
            |> List.sequenceResultA
            |> Result.mapError Misc.flattenList

        let! consts = 
            astData.Consts
            |> getValues
            |> List.map (tryResolveConst astData)
            |> List.sequenceResultA
            |> Result.mapError Misc.flattenList

        let! methods = 
            astData.Methods
            |> getValues
            |> List.map (tryResolveMethodWithoutScope astData (Misc.mapFromKey (fun x -> x.Name) resolvedStructs) (Misc.mapFromKey (fun x -> x.Name) resolvedWrapperTypes))
            |> List.sequenceResultA
            |> Result.mapError Misc.flattenList

        let importedModules = 
            astData.ImportedModules
            |> Map.map (fun k (moduleName, identifierName) -> 
                ({
                    ModuleName = moduleName
                    IdentifierName = identifierName
                }):ResolvedAst.ImportDeclarationData
                )

        return (({
            BuiltInFunctions = Globals.builtInFunctions |> Misc.mapFromKey (_.Name)
            Structs = resolvedStructs |> Misc.mapFromKey (_.Name)
            Interfaces = resolvedInterfaces |> Misc.mapFromKey (_.Name)
            Aliases = resolvedAliases |> Misc.mapFromKey (_.Name)
            WrapperTypes = resolvedWrapperTypes |> Misc.mapFromKey (_.Name)
            Functions = resolvedFunctions |> Misc.mapFromKey (_.Name)
            Consts = consts |> Misc.mapFromKey (_.IdentifierName)
            ImportedModules = importedModules
            Enums = resolvedEnums |> Misc.mapFromKey (_.Name)
            Methods = methods |> Misc.mapFromKey (fun x ->
                match x.ReceiverType with
                | ResolvedAst.MethodReceiverType.WrapperTypeReceiver({Name = receiverTypeName}, _) -> receiverTypeName, x.Name
                | ResolvedAst.MethodReceiverType.StructReceiver({Name = receiverTypeName}, _) -> receiverTypeName, x.Name
                )

        }):ResolvedAst.ResolvedDeclarations)
    }

let tryResolveToplevelDeclaration (toplevelContext:ResolvedAst.Context) (toplevelDeclaration:Ast.ToplevelDeclaration) = 
    match toplevelDeclaration with
    | Ast.ToplevelDeclaration.Alias(_, aliasName, _, _) ->
        result {
            return
                toplevelContext.ResolvedDeclarations.Aliases
                |> Map.find aliasName 
                |> ResolvedAst.ToplevelDeclaration.Alias
        }
    | Ast.ToplevelDeclaration.Enum(name, _) -> result {
        return
            toplevelContext.ResolvedDeclarations.Enums
            |> Map.find name
            |> ResolvedAst.ToplevelDeclaration.Enum 
        }
    | Ast.ToplevelDeclaration.Const(_, identifierName, _) ->
        result {
            return
                toplevelContext.ResolvedDeclarations.Consts
                |> Map.find identifierName
                |> ResolvedAst.ToplevelDeclaration.Const
        }
    | Ast.ToplevelDeclaration.Function(_, functionName, generics, parameters, _, scopeData) ->
        result {
            let declaredFunctionData = 
                toplevelContext.ResolvedDeclarations.Functions
                |> Map.find functionName
            
            let! createdVariables =
                parameters
                |> List.map(fun (hasEllipsis, unresolvedType, identifierName) -> result {
                    let! resolvedType = tryResolveType toplevelContext.AstData generics unresolvedType
                    return createParameterVariable identifierName hasEllipsis resolvedType
                })
                |> List.sequenceResultA
                |> Result.mapError Misc.flattenList


            let! resolvedScope, _ = 
                tryResolveScope toplevelContext createdVariables generics [] scopeData 
            
            return ({
                ExportedStatus = declaredFunctionData.ExportedStatus
                Name = declaredFunctionData.Name
                Generics = declaredFunctionData.Generics
                Parameters = declaredFunctionData.Parameters
                ReturnType = declaredFunctionData.ReturnType
                ScopeData = resolvedScope
            }:ResolvedAst.FunctionDeclarationData)
            |> ResolvedAst.ToplevelDeclaration.Function 
        }
    | Ast.ToplevelDeclaration.Import(moduleName, identifierName) ->
        result {
            return
                toplevelContext.ResolvedDeclarations.ImportedModules
                |> Map.find identifierName
                |> ResolvedAst.ToplevelDeclaration.Import
        }
    | Ast.ToplevelDeclaration.Interface(_, interfaceName, _, _) ->
        result {
            return
                toplevelContext.ResolvedDeclarations.Interfaces
                |> Map.find interfaceName
                |> ResolvedAst.ToplevelDeclaration.Interface
        }
    | Ast.ToplevelDeclaration.Method(_, methodName, (((receiverTypeName, _) as unresolvedReceiverType, receiverParameterName), normalParameters), _, rawScopeData) ->
        result {
            let declaredMethodData =
                toplevelContext.ResolvedDeclarations.Methods
                |> Map.find (receiverTypeName, methodName)
            let! resolvedReceiverType = tryResolveType toplevelContext.AstData [] (UnresolvedType.PendingResolutionType(unresolvedReceiverType))
            // Uncomment if needed
            //let! receiverParameterGenerics = tryResolveGenericsInstantiation toplevelContext.AstData receiverTypeInstantiationGenerics
            let receiverParameter = [createParameterVariable receiverParameterName false resolvedReceiverType]
            let! createdVariables =
                normalParameters
                |> List.map(fun (hasEllipsis, unresolvedType, identifierName) -> result {
                    let! resolvedType = tryResolveType toplevelContext.AstData [] unresolvedType
                    return createParameterVariable identifierName hasEllipsis resolvedType
                })
                |> List.sequenceResultA
                |> Result.mapError Misc.flattenList
            

            let! resolvedScopeData, _= tryResolveScope toplevelContext (createdVariables @ receiverParameter) [] [] rawScopeData
            return
                (({
                    Name = declaredMethodData.Name
                    ExportedStatus = declaredMethodData.ExportedStatus
                    ReceiverType = declaredMethodData.ReceiverType
                    Parameters = declaredMethodData.Parameters
                    ReturnType = declaredMethodData.ReturnType
                    ScopeData = resolvedScopeData
                    ReceiverParameterName = receiverParameterName
                }):ResolvedAst.MethodDeclarationData)
                |> ResolvedAst.ToplevelDeclaration.Method

        }
    | Ast.ToplevelDeclaration.WrapperType(_, wrapperTypeName, _, _) ->
        result {
            return
                toplevelContext.ResolvedDeclarations.WrapperTypes
                |> Map.find wrapperTypeName
                |> ResolvedAst.ToplevelDeclaration.WrapperType
        }
    | Ast.ToplevelDeclaration.Struct(_, structName, _, _) ->
        result {
            return
                toplevelContext.ResolvedDeclarations.Structs
                |> Map.find structName
                |> ResolvedAst.ToplevelDeclaration.Struct
                
        }
        


