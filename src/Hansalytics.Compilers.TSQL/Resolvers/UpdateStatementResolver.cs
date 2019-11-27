﻿using Hansalytics.Compilers.TSQL.Models;
using Hansalytics.Compilers.TSQL.Exceptions;
using Hansalytics.Compilers.TSQL.Helpers;
using System;
using System.Collections.Generic;
using TSQL.Tokens;
using Hansalytics.Compilers.TSQL.Models.DataEntities;

namespace Hansalytics.Compilers.TSQL.Resolvers
{
    class UpdateStatementResolver : IDataManipulationResolver
    {
        DataManipulation manipulation;

        //States who many database objects are added to the current scope 
        //by this statement (subselects are excluded). At the end of the statement
        //as many objects are poped from the context stack
        int objectsAddedToContext = 0;

        public DataManipulation Resolve(ReadOnlySpan<TSQLToken> tokens, ref int fileIndex, CompilerContext context)
        {
            manipulation = new DataManipulation();

            fileIndex++; //skip 'update'

            var targetObject = StatementResolveHelper.ResolveDatabaseObject(tokens, ref fileIndex, context, true);

            fileIndex++; //skip 'set'

            do
            {
                var target = StatementResolveHelper.ResolveExpression(tokens, ref fileIndex, context); //resolve target column

                AddTargetObject(target, targetObject);

                fileIndex++; //skip '='

                var source = StatementResolveHelper.ResolveExpression(tokens, ref fileIndex, context); //resolve source column
                target.ChildExpressions.Add(source);

                manipulation.Expressions.Add(target);

                if (fileIndex < tokens.Length && tokens[fileIndex].Text.Equals(","))
                {
                    fileIndex++; //skip ','
                    continue;
                }
                else
                    break;

            } while (true);

            var objects = StatementResolveHelper.ResolveFromStatement(tokens, ref fileIndex, context);

            AddObjectsToContext(objects, context);

            var beautified = new List<Expression>();

            foreach(var expr in manipulation.Expressions)
                beautified.Add(StatementResolveHelper.BeautifyColumns(expr, context));

            manipulation.Expressions = beautified;

            StatementResolveHelper.ResolveWhereStatement(tokens, ref fileIndex, context);

            PopObjectsFromContextStack(context);

            return manipulation;
        }

        /// <summary>
        /// Adds the target object to a target expression if it is not explicitly
        /// mentioned
        /// </summary>
        private void AddTargetObject(Expression target, DatabaseObject targetObject)
        {
            if (target.Type != ExpressionType.COLUMN)
                throw new ArgumentException("Expression has to be a column");

            Helper.SplitColumnNotationIntoSingleParts(target.Name, out string databaseName, out string databaseSchema, out string databaseObjectName, out string columnName);

            if (databaseObjectName == null)
            {
                databaseObjectName = targetObject.Name;
                target.Name = databaseObjectName + "." + columnName;
            }

            if (databaseSchema == null && targetObject.Schema != null)
            {
                databaseSchema = targetObject.Schema;
                target.Name = databaseSchema + "." + target.Name;
            }
            else
                return;

            if (databaseName == null && targetObject.Database != null)
            {
                databaseName = targetObject.Database;
                target.Name = databaseName + "." + target.Name;
            }
        }

        /// <summary>
        /// Pops as many database objects from the stack as this statement pushed
        /// </summary>
        private void PopObjectsFromContextStack(CompilerContext context)
        {
            while (objectsAddedToContext > 0)
            {
                context.CurrentDatabaseObjectContext.Pop();
                objectsAddedToContext--;
            }
        }

        private void AddObjectsToContext(List<DatabaseObject> objects, CompilerContext context)
        {
            foreach (var obj in objects)
            {
                context.AddDatabaseObjectToCurrentContext(obj);
            }

            objectsAddedToContext = objects.Count;
        }
    }
}