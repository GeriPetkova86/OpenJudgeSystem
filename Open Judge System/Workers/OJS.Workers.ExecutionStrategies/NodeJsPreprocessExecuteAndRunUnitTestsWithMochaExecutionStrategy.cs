﻿namespace OJS.Workers.ExecutionStrategies
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using OJS.Workers.Common;
    using OJS.Workers.Checkers;
    using Newtonsoft.Json.Linq;

    public class NodeJsPreprocessExecuteAndRunUnitTestsWithMochaExecutionStrategy : NodeJsPreprocessExecuteAndCheckExecutionStrategy
    {
        private string mochaModulePath;
        private string chaiModulePath;

        public NodeJsPreprocessExecuteAndRunUnitTestsWithMochaExecutionStrategy(string nodeJsExecutablePath, string mochaModulePath, string chaiModulePath)
            : base(nodeJsExecutablePath)
        {
            if (!File.Exists(nodeJsExecutablePath))
            {
                throw new ArgumentException(string.Format("Mocha not found in: {0}", nodeJsExecutablePath), "mochaModulePath");
            }

            if (!Directory.Exists(chaiModulePath))
            {
                throw new ArgumentException(string.Format("Chai not found in: {0}", nodeJsExecutablePath), "chaiModulePath");
            }

            this.mochaModulePath = mochaModulePath;
            this.chaiModulePath = chaiModulePath.Replace('\\', '/');
        }

        protected override string JsCodeRequiredModules
        {
            get
            {
                return @"
var chai = require('" + chaiModulePath + @"'),
	assert = chai.assert;
	expect = chai.expect,
	should = chai.should();";
            }
        }

        protected override string JsCodePreevaulationCode
        {
            get
            {
                return @"
describe('TestScope', function() {
	it('Test', function(done) {
		var content = '';";
            }
        }

        protected override string JsCodeEvaluation
        {
            get
            {
                return @"
    var inputData = content.trim();
    var result = code.run();
    if (result == undefined) {
        result = 'Invalid!';
    }
	
	testFunc = new Function('assert', 'expect', 'should', ""var result = this.valueOf();"" + inputData);
    testFunc.call(result, assert, expect, should);
	done();";
            }
        }

        protected override string JsCodePostevaulationCode
        {
            get
            {
                return @"
    });
});";
            }
        }

        protected override List<TestResult> ProcessTests(ExecutionContext executionContext, IExecutor executor, IChecker checker, string codeSavePath)
        {
            var testResults = new List<TestResult>();

            var arguments = new List<string>();
            arguments.Add(this.mochaModulePath);
            arguments.Add(codeSavePath);
            arguments.AddRange(executionContext.AdditionalCompilerArguments.Split(' '));

            foreach (var test in executionContext.Tests)
            {
                var processExecutionResult = executor.Execute(this.NodeJsExecutablePath, test.Input, executionContext.TimeLimit, executionContext.MemoryLimit, arguments);

                JObject jsonTestResult = null;
                var passed = false;
                string error = null;

                try
                {
                    jsonTestResult = JObject.Parse(processExecutionResult.ReceivedOutput.Trim());
                    passed = (int)jsonTestResult["stats"]["passes"] == 1;
                }
                catch
                {
                    error = "Invalid console output!";
                }

                if (!passed)
                {
                    try
                    {
                        error = (string)jsonTestResult["failures"][0]["err"]["message"];
                    }
                    catch
                    {
                        error = "Invalid console output!";
                    }
                }
                
                var testResult = this.ExecuteAndCheckTest(test, processExecutionResult, checker, passed ? "yes" : error);
                testResults.Add(testResult);
            }

            return testResults;
        }
    }
}