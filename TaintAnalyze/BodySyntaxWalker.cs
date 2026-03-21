using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.TaintAnalyze
{
    internal class BodySyntaxWalker(SemanticModel model) : CSharpSyntaxWalker
    {
        private readonly SemanticModel semanticModel = model;

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            
        }
    }
}
