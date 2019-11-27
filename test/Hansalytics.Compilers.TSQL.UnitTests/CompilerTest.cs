﻿using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Hansalytics.Compilers.TSQL.UnitTests
{
    public class CompilerTest
    {
        [Fact]
        public void ShouldCompileSQL()
        {
            //Arrange
            string rawTsql = "SELECT justAColumn FROM testTable " +
                "UPDATE targetTable t SET t.col01 = s.col1337 FROM testTable s";
            Compiler compiler = new Compiler();

            //Act
            var result = compiler.Compile(rawTsql, "stdServer", "stdDb", "xUnit");

            //Assert
            //Assert.Equal()
        }
    }
}
