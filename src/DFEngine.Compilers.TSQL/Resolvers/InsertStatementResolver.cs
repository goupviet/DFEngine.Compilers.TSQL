﻿using DFEngine.Compilers.TSQL.Models;
using DFEngine.Compilers.TSQL.Exceptions;
using DFEngine.Compilers.TSQL.Helpers;
using System;
using System.Collections.Generic;
using TSQL.Tokens;
using DFEngine.Compilers.TSQL.Constants;

namespace DFEngine.Compilers.TSQL.Resolvers
{
    /// <summary>
    /// Resolves an INSERT statement
    /// </summary>
    /// <see href="https://docs.microsoft.com/en-us/sql/t-sql/statements/insert-transact-sql?view=sql-server-ver15"/>
    class InsertStatementResolver : IDataManipulationResolver
    {
        DataManipulation manipulation;

        //States who many database objects are added to the current scope 
        //by this statement (subselects are excluded). At the end of the statement
        //as many objects are poped from the context stack
        //int objectsAddedToContext = 0;

        DatabaseObject targetObject;

        List<Expression> targets = new List<Expression>();

        List<Expression> sources = new List<Expression>();

        public DataManipulation Resolve(ReadOnlySpan<TSQLToken> tokens, ref int fileIndex, CompilerContext context)
        {
            manipulation = new DataManipulation();

            fileIndex++; //skip 'insert'

            //TODO Resolve TOP Expression

            if (tokens[fileIndex].Text.ToLower().Equals("into"))
                fileIndex++; //skip 'into'

            targetObject = StatementResolveHelper.ResolveDatabaseObject(tokens, ref fileIndex, context, true);

            DetermineTargetColumns(tokens, ref fileIndex, context);

            //TODO Resolve Table Hint

            if (tokens[fileIndex].Text.Equals("values", StringComparison.InvariantCultureIgnoreCase))
            {
                fileIndex += 2; //skip 'values ('
                DetermineSourceObjects(tokens, ref fileIndex, context);

            }
            else if(tokens[fileIndex].Text.Equals("select", StringComparison.InvariantCultureIgnoreCase))
            {
                var selectStatement = new SelectStatementResolver().Resolve(tokens, ref fileIndex, context);
                sources = selectStatement.ChildExpressions;
            }
            
            if(targets.Count == 0)
            {
                for (int index = 0; index < sources.Count; index++)
                {
                    Expression resolvedSource;

                    if (sources[index].Type.Equals(ExpressionType.COMPLEX) || sources[index].Type.Equals(ExpressionType.CONSTANT))
                        continue;
                    else if (sources[index].Type.Equals(ExpressionType.ALIAS))
                        resolvedSource = sources[index].ChildExpressions[0];
                    else
                        resolvedSource = sources[index];

                    var singleManipulation = new Expression(ExpressionType.COLUMN)
                    {
                        Name = Beautifier.EnhanceNotation(targetObject, InternalConstants.UNRELATED_COLUMN_NAME)
                    };

                    singleManipulation.ChildExpressions.Add(resolvedSource);
                    manipulation.Expressions.Add(singleManipulation);
                }
            }
            else if(StatementResolveHelper.HaveEqualAmountOfRealExpression(sources, targets))
            {
                for (int index = 0; index < targets.Count; index++)
                {
                    if (!sources[index].Type.Equals(ExpressionType.COLUMN) && !sources[index].Type.Equals(ExpressionType.SCALAR_FUNCTION))
                        continue;

                    var singleManipulation = new Expression(ExpressionType.COLUMN)
                    {
                        Name = targets[index].Name
                    };

                    singleManipulation.ChildExpressions.Add(sources[index]);
                    manipulation.Expressions.Add(singleManipulation);
                }
            }
            else
                throw new InvalidSqlException("Amount of targets does not match the number of sources");

            if (fileIndex < tokens.Length && tokens[fileIndex].Text.Equals(";"))
                fileIndex++;

            var beautified = new List<Expression>();

            foreach (var exp in manipulation.Expressions)
                beautified.Add(Beautifier.BeautifyColumns(exp, context));

            manipulation.Expressions = beautified;

            return manipulation;
        }

        private void DetermineTargetColumns(ReadOnlySpan<TSQLToken> tokens, ref int fileIndex, CompilerContext context)
        {
            if (tokens[fileIndex].Text.Equals("("))
                fileIndex++; // skip '('
            else
                return;

            do
            {
                var target = StatementResolveHelper.ResolveExpression(tokens, ref fileIndex, context);
                AddTargetObject(target);
                targets.Add(target);

                if (tokens[fileIndex].Text.Equals(","))
                {
                    fileIndex++; //skip ','
                    continue;
                }
                else
                    break;
                
            } while (true);

            fileIndex++; //skip ')'

            return;
        }

        private void DetermineSourceObjects(ReadOnlySpan<TSQLToken> tokens, ref int fileIndex, CompilerContext context)
        {
            do
            {
                var source = StatementResolveHelper.ResolveExpression(tokens, ref fileIndex, context);
                sources.Add(source);

                if (fileIndex < tokens.Length && tokens[fileIndex].Text.Equals(","))
                {
                    fileIndex++; //skip ','
                    continue;
                }
                else
                    break;

            } while (true);

            fileIndex++; //skip ')'
        }

        /// <summary>
        /// Adds the target object to a target expression if it is not explicitly mentioned
        /// </summary>
        private void AddTargetObject(Expression target)
        {
            if (target.Type != ExpressionType.COLUMN)
                throw new ArgumentException("Expression has to be a column");

            Helper.SplitColumnNotationIntoSingleParts(target.Name, out string databaseName, out string databaseSchema, out string databaseObjectName, out string columnName, true);

            target.Name = columnName;

            if (databaseObjectName == null)
            {
                databaseObjectName = targetObject.Name;
                target.Name = databaseObjectName + "." + target.Name;
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
    }
}
