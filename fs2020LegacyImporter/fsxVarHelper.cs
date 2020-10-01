using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace msfsLegacyImporter
{
    class fsxVarHelper
    {
        public string fsx2msfsSimVar(string fsxSimVar)
        {
            fsxSimVar = Regex.Replace(fsxSimVar, "\r\n|\r|\n", "");
            fsxSimVar = Regex.Replace(fsxSimVar, @"\s\s+", " ");
            /*switch (fsxSimVar.Trim())
            {
                case "(A:Airspeed select indicated or true,knots)":
                    return "var ExpressionResult = parseFloat(SimVar.GetSimVarValue(\"AIRSPEED TRUE\", \"knots\"));";
                case "(A:Vertical speed,feet per minute) 0.00988 *":
                    return "var ExpressionResult = 0.00988 * parseFloat(SimVar.GetSimVarValue(\"VERTICAL SPEED\", \"feet per minute\"));";
                case "(P:Units of measure, enum) 2 == if{ (A:Indicated Altitude, meters) } els{ (A:Indicated Altitude, feet) } 100000 / 360 * dgrd":
                    return "var ExpressionResult = parseFloat(SimVar.GetSimVarValue(\"INDICATED ALTITUDE:2\", \"feet\")) * 360 / 100000 / 180 * Math.PI;";
                case "(P:Units of measure, enum) 2 == if{ (A:Indicated Altitude, meters) } els{ (A:Indicated Altitude, feet) } 10000 / 360 * dgrd":
                    return "var ExpressionResult = parseFloat(SimVar.GetSimVarValue(\"INDICATED ALTITUDE:2\", \"feet\")) * 360 / 10000 / 180 * Math.PI;";
                case "(P:Units of measure, enum) 2 == if{ (A:Indicated Altitude, meters) } els{ (A:Indicated Altitude, feet) } 1000 / 360 * 90 + dgrd":
                    return "var ExpressionResult = parseFloat(SimVar.GetSimVarValue(\"INDICATED ALTITUDE:2\", \"feet\")) * 360 / 1000 / 180 * Math.PI;";
                case "(A:TURN COORDINATOR BALL,percent)":
                    return "var ExpressionResult = -parseFloat(SimVar.GetSimVarValue(\"ATTITUDE INDICATOR BANK DEGREES\", \"degree\")) / 30 * 100;";
                case "(A:Delta Heading Rate, rpm) 0.44 * (A:ELECTRICAL MASTER BATTERY,bool) *":
                    return "var ExpressionResult = parseFloat(SimVar.GetSimVarValue(\"VELOCITY BODY X\", \"feet per second\")) / 180 * Math.PI;";
                case "(A:Variometer rate, knots)":
                    return "var ExpressionResult = parseFloat(SimVar.GetSimVarValue(\"AIRCRAFT WIND Y\", \"knots\"));"; // TODO: fix rate
                case "(A:Wiskey compass indication degrees,degrees) dnor 244 * 360 / 269 -":
                    return "var ExpressionResult = (-360 + parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES MAGNETIC\", \"degree\")) - 30) * 244 / 360;";
                case "(A:Magnetic compass,radians) /-/":
                    return "var ExpressionResult = - parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES MAGNETIC\", \"degree\")) / 180 * Math.PI;";
                case "(A:Fuel tank center level,position)":
                    return "var ExpressionResult = parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL QUANTITY\", \"gallons\")) / parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL CAPACITY\", \"gallons\"));";
                case "(A:AMBIENT TEMPERATURE,Celsius) 75 / 243 * 150.5 -":
                    return "var ExpressionResult = parseFloat(SimVar.GetSimVarValue(\"AMBIENT TEMPERATURE\", \"celsius\")) * 243 / 75 - 150.5";
                case "(A:Kohlsman setting hg,inHg) 28.1 - -197 * 3.4 / 0.8 +":
                    return "var ExpressionResult = - (parseFloat(SimVar.GetSimVarValue(\"KOHLSMAN SETTING HG: 2\", \"inches of mercury\")) - 28.1) * 197 / 3.4;";
            }*/

            string fsxVariable = fsxSimVar;
            List<string[]> variables = new List<string[]>();

            // REPLACE VARIABLES
            Random r = new Random();
            //var regex = new Regex(@"(.*).*{.*}.*{.*}|\((.*?)\)");
            var regex = new Regex(@"\((.*?)\)");
            foreach (var match in regex.Matches(fsxVariable))
            {
                string placeholder = r.Next(1000000, 9000000).ToString();
                variables.Add(new string[] { match.ToString(), placeholder });
                fsxVariable = fsxVariable.Replace(match.ToString(), placeholder);
            }

            // PARSE FSX FORMULA
            string infix = PostfixToInfix(fsxVariable);

            // INSERT VARIABLES
            if (!String.IsNullOrEmpty(infix))
            {
                foreach (string[] variable in variables)
                {
                    string msfsVariable = getMsfsVariable(variable[0]);
                    infix = infix.Replace(variable[1], msfsVariable);

                    if (msfsVariable == "0")
                        Console.WriteLine("FSX variable match not found: " + variable[0]);
                }

                // REMOVE TRALING SYMBOLS
                infix = infix.Trim();
                if (infix[infix.Length - 1] == '+' || infix[infix.Length - 1] == '*' || infix[infix.Length - 1] == '/' || infix[infix.Length - 1] == '-')
                    infix = infix.Substring(infix.Length - 1).Trim();

                Console.WriteLine("Orig: " + fsxSimVar + " / Final: " + infix);

                if (!String.IsNullOrEmpty(infix) && infix.Length > 1)
                    return "var ExpressionResult = " + infix + ";";
            }

            return "var ExpressionResult = 0; /* SIM VAR \"" + fsxSimVar + "\" NOT PARSED! */";
        }

        private string getMsfsVariable(string fsxVariable)
        {
            switch (fsxVariable.Trim().Replace("( ", "(").Replace(" )", ")").Replace(" ,", ",").Replace("{ ", "{").Replace(" {", "{").Replace("} ", "}").Replace(" }", "}").Replace(": ", ":").Replace(" :", ":"))
            {
                case string hn when hn.Equals("(A:Airspeed select indicated or true,knots)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRSPEED TRUE\", \"knots\"))";
                case string hn when hn.Equals("(A:Vertical speed,feet per minute)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VERTICAL SPEED\", \"feet per minute\"))";
                case string hn when hn.Equals("(A:Vertical speed,meters per minute)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"VERTICAL SPEED\", \"meters per minute\"))";
                case string hn when hn.Equals("(A:Indicated Altitude, meters)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"INDICATED ALTITUDE:2\", \"meters\"))";
                case string hn when hn.Equals("(A:Indicated Altitude, feet)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"INDICATED ALTITUDE:2\", \"feet\"))";
                case string hn when hn.Equals("(A:TURN COORDINATOR BALL,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "Math.min(30, Math.max(-30 , -parseFloat(SimVar.GetSimVarValue(\"ATTITUDE INDICATOR BANK DEGREES\", \"degrees\")))) / 30 * 100";
                case string hn when hn.Equals("(A:Delta Heading Rate, rpm)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(-SimVar.GetSimVarValue(\"TURN INDICATOR RATE\", \"degree per second\")) * 60 / 360";
                case string hn when hn.Equals("(A:Variometer rate, knots)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRCRAFT WIND Y\", \"knots\"))"; // TODO: fix rate
                case string hn when hn.Equals("(A:Wiskey compass indication degrees,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES MAGNETIC\", \"degrees\"))";
                case string hn when hn.Equals("(A:Magnetic compass,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES MAGNETIC\", \"radians\"))";
                case string hn when hn.Equals("(A:Fuel tank total level,position)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:Fuel tank center level,position)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL QUANTITY\", \"gallons\")) / parseFloat(SimVar.GetSimVarValue(\"FUEL TOTAL CAPACITY\", \"gallons\"))";
                case string hn when hn.Equals("(A:AMBIENT TEMPERATURE,Celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AMBIENT TEMPERATURE\", \"celsius\"))";
                case string hn when hn.Equals("(A:Kohlsman setting hg,inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"KOHLSMAN SETTING HG: 2\", \"inches of mercury\"))";
                case string hn when hn.Equals("(A:ELECTRICAL MASTER BATTERY,bool)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:PARTIAL PANEL ELECTRICAL, enum)", StringComparison.InvariantCultureIgnoreCase):
                    return "1";
                case string hn when hn.Equals("(P:Units of measure, enum)", StringComparison.InvariantCultureIgnoreCase):
                    return "1";
                case string hn when hn.Equals("(A:AIRSPEED INDICATED,mph)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AIRSPEED INDICATED\", \"mph\"))";//???
                case string hn when hn.Equals("(A:SUCTION PRESSURE, inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"SUCTION PRESSURE\", \"inch of mercury\"))";
                case string hn when hn.Equals("(A:GENERAL ENG1 OIL PRESSURE, PSI)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE: 1\", \"psi\"))";
                case string hn when hn.Equals("(A:GENERAL ENG2 OIL PRESSURE, PSI)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL PRESSURE: 2\", \"psi\"))";
                case string hk when hk.Equals("(A:GENERAL ENG1 OIL TEMPERATURE, celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL TEMPERATURE: 1\", \"celsius\"))";
                case string hk when hk.Equals("(A:GENERAL ENG2 OIL TEMPERATURE, celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG OIL TEMPERATURE: 2\", \"celsius\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:1,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:1\", \"psi\"))";
                case string hn when hn.Equals("(A:General Eng Fuel Pressure:2,psi)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG FUEL PRESSURE:2\", \"psi\"))";
                case string hn when hn.Equals("(A:RADIO HEIGHT, meters)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"RADIO HEIGHT\", \"meters\"))";
                case string hn when hn.Equals("(A:RADIO HEIGHT, feet)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"RADIO HEIGHT\", \"feet\"))";
                case string hn when hn.Equals("(A:DECISION HEIGHT, feet)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"DECISION HEIGHT\", \"feet\"))";
                case string hn when hn.Equals("(A:DECISION HEIGHT, meters)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"DECISION HEIGHT\", \"meters\"))";
                case string hn when hn.Equals("(P:Local time,seconds)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetGlobalVarValue(\"LOCAL TIME\", \"seconds\"))";
                case string hn when hn.Equals("(P:Local time,minutes)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetGlobalVarValue(\"LOCAL TIME\", \"minutes\"))";
                case string hn when hn.Equals("(P:Local time,hours)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetGlobalVarValue(\"LOCAL TIME\", \"hours\"))";
                case string hn when hn.Equals("(A:GENERAL ENG1 RPM, rpm)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG RPM: 2\", \"Rpm\"))";
                case string hn when hn.Equals("(A:GENERAL ENG2 RPM, rpm)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG RPM: 1\", \"Rpm\"))";
                case string hn when hn.Equals("(A:Plane heading degrees gyro,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES TRUE\", \"degrees\"))";//???
                case string hn when hn.Equals("(A:PLANE HEADING DEGREES GYRO, radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES TRUE\", \"radians\"))";
                case string hn when hn.Equals("(A:Autopilot heading lock dir,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"AUTOPILOT HEADING LOCK DIR\", \"degrees\"))";//???
                case string hn when hn.Equals("(A:RUDDER TRIM PCT, percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"RUDDER TRIM PCT\", \"percent\"))";
                case string hn when hn.Equals("(A:ELEVATOR TRIM POSITION, degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELEVATOR TRIM PCT\", \"percent\"))";
                case string hn when hn.Equals("(A:NAV GSI:1,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV GSI:1\", \"percent\"))";
                case string hn when hn.Equals("(A:NAV GSI:2,percent)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV GSI:2\", \"percent\"))";
                case string hn when hn.Equals("(A:PARTIAL PANEL HEADING,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PARTIAL PANEL HEADING\", \"bool\"))";
                case string hn when hn.Equals("(A:PARTIAL PANEL ELECTRICAL,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PARTIAL PANEL ELECTRICAL\", \"bool\"))";
                case string hn when hn.Equals("(A:TRAILING EDGE FLAPS LEFT ANGLE,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"TRAILING EDGE FLAPS RIGHT ANGLE\", \"radians\"))";
                case string hn when hn.Equals("(A:ENG1 MANIFOLD PRESSURE, inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG MANIFOLD PRESSURE: 1\", \"inHG\"))";
                case string hn when hn.Equals("(A:ENG2 MANIFOLD PRESSURE, inHg)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG MANIFOLD PRESSURE: 2\", \"inHG\"))";
                case string hn when hn.Equals("(A:Attitude indicator bank degrees,radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ATTITUDE INDICATOR BANK DEGREES\", \"radians\"))";
                case string hn when hn.Equals("(L:Show Volts 1,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "1";//"parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL MAIN BUS VOLTAGE:1\", \"volts\"))";
                case string hn when hn.Equals("(L:Show Volts 2,bool)", StringComparison.InvariantCultureIgnoreCase):
                    return "1";//parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL MAIN BUS VOLTAGE:2\", \"volts\"))";
                case string hn when hn.Equals("(A:RECIP CARBURETOR TEMPERATURE:1, celsius)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:GENERAL ENG1 OIL TEMPERATURE, celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG OIL TEMPERATURE:1\", \"celsius\")) * 0.5";//??
                case string hn when hn.Equals("(A:RECIP CARBURETOR TEMPERATURE:2, celsius)", StringComparison.InvariantCultureIgnoreCase):
                case string hk when hk.Equals("(A:GENERAL ENG2 OIL TEMPERATURE, celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"GENERAL ENG OIL TEMPERATURE:2\", \"celsius\")) * 0.5";//??
                case string hn when hn.Equals("(A:RECIP ENG CYLINDER HEAD TEMPERATURE:1, celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG CYLINDER HEAD TEMPERATURE:1\", \"celsius\"))";
                case string hn when hn.Equals("(A:RECIP ENG CYLINDER HEAD TEMPERATURE:2, celsius)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ENG CYLINDER HEAD TEMPERATURE:2\", \"celsius\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS VOLTAGE:1, volts)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS VOLTAGE:1\", \"volts\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS VOLTAGE:2, volts)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS VOLTAGE:2\", \"volts\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS AMPS:1, amps)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS AMPS:1\", \"amperes\"))";
                case string hn when hn.Equals("(A:ELECTRICAL GENALT BUS AMPS:2, amps)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ELECTRICAL GENALT BUS AMPS:2\", \"amperes\"))";
                case string hn when hn.Equals("(A:PLANE HEADING DEGREES GYRO, radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"PLANE HEADING DEGREES TRUE\", \"radians\"))";
                case string hn when hn.Equals("(A:NAV1 OBS,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV OBS:1\", \"degrees\"))";
                case string hn when hn.Equals("(A:NAV2 OBS,degrees)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"NAV OBS:2\", \"degrees\"))";
                case string hn when hn.Equals("(A:ADF radial:1, radians)", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat(SimVar.GetSimVarValue(\"ADF RADIAL:1\", \"radians\"))";
                case string hn when hn.Equals("", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat()";
                case string hn when hn.Equals("", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat()";
                case string hn when hn.Equals("", StringComparison.InvariantCultureIgnoreCase):
                    return "parseFloat()";
            }

            return "0";
        }

        // POSTFIX CONVERTER BY W. Michael Perkins STARTS
        public class Intermediate
        {
            public string expr;     // subexpression string
            public string oper;     // the operator used to create this expression

            public Intermediate(string expr, string oper)
            {
                this.expr = expr;
                this.oper = oper;
            }
        }

        static string PostfixToInfix(string postfix)
        {
            try
            {
                int ifElseState = 0;

                // Assumption: the postfix expression to be processed is space-delimited.
                // Split the individual tokens into an array for processing.
                postfix = postfix.Replace("if {", "if{").Replace("} els", "}els").Replace("els {", "els{");

                var postfixTokens = postfix.Split(' ');

                // Create stack for holding intermediate infix expressions
                var stack = new Stack<Intermediate>();

                foreach (string token in postfixTokens)
                {
                    string newExpr;

                    Console.WriteLine("Token: " + token);

                    if (token == "==" || token == "!=" || token == ">" || token == "<" || token == ">=" || token == "<=")
                    {
                        var rightIntermediate = stack.Pop();
                        var leftIntermediate = stack.Pop();
                        newExpr = "( " + leftIntermediate.expr + " " + token + " " + rightIntermediate.expr + " )";
                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else if (token.ToLower() == "if{")
                    {
                        ifElseState = 1;

                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = leftIntermediate.expr + " ( " + rightIntermediate.expr + " ? ";
                        }
                        else if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = "( " + rightIntermediate.expr + " ? ";
                        }
                        else
                        {
                            newExpr = "";
                        }

                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else if (token.ToLower() == "}els{")
                    {
                        var rightIntermediate = stack.Pop();
                        var leftIntermediate = stack.Pop();
                        newExpr = leftIntermediate.expr + " " + rightIntermediate.expr + " : ";
                        stack.Push(new Intermediate(newExpr, token));

                        ifElseState = 0;
                    }
                    else if (token == "}")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = leftIntermediate.expr + " " + rightIntermediate.expr + (ifElseState == 1 ? " : \"\"" : "") + " )";
                            stack.Push(new Intermediate(newExpr, token));
                        }
                        else if (stack.Count > 0)
                        {
                            var leftIntermediate = stack.Pop();
                            newExpr = leftIntermediate.expr + (ifElseState == 1 ? " : \"\"" : "") + " )";
                            stack.Push(new Intermediate(newExpr, token));
                        }

                        ifElseState = 0;
                    }
                    else if (token == "||" || token.ToLower() == "or")
                    {
                        var rightIntermediate = stack.Pop();
                        var leftIntermediate = stack.Pop();
                        newExpr = leftIntermediate.expr + " || " + rightIntermediate.expr;
                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else if (token == "!" || token.ToLower() == "not")
                    {
                        var leftIntermediate = stack.Pop();
                        stack.Push(new Intermediate("!" + leftIntermediate.expr, token));
                    }
                    else if (token == "/-/")
                    {
                        if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = rightIntermediate.expr + " * (-1)";
                        }
                        else
                        {
                            newExpr = "(-1)";
                        }

                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else if (token == "+" || token == "-")
                    {
                        if (stack.Count > 1)
                        {
                            var rightIntermediate = stack.Pop();
                            var leftIntermediate = stack.Pop();
                            newExpr = leftIntermediate.expr + " " + token + " " + rightIntermediate.expr;
                        }
                        else if (stack.Count > 0)
                        {
                            var rightIntermediate = stack.Pop();
                            newExpr = rightIntermediate.expr + " " + token;
                        }
                        else
                        {
                            newExpr = token;
                        }

                        // Push the new intermediate expression on the stack
                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else if (token == "*" || token == "/" || token == "%")
                    {
                        string leftExpr, rightExpr;

                        // Get the intermediate expressions from the stack.  
                        // If an intermediate expression was constructed using a lower precedent
                        // operator (+ or -), we must place parentheses around it to ensure 
                        // the proper order of evaluation.

                        var rightIntermediate = stack.Pop();
                        if (rightIntermediate.oper == "+" || rightIntermediate.oper == "-")
                        {
                            rightExpr = "(" + rightIntermediate.expr + ")";
                        }
                        else
                        {
                            rightExpr = rightIntermediate.expr;
                        }

                        if (stack.Count > 0)
                        {
                            var leftIntermediate = stack.Pop();
                            if (leftIntermediate.oper == "+" || leftIntermediate.oper == "-")
                            {
                                leftExpr = "(" + leftIntermediate.expr + ")";
                            }
                            else
                            {
                                leftExpr = leftIntermediate.expr;
                            }
                        }
                        else
                        {
                            leftExpr = "";
                        }

                        // construct the new intermediate expression by combining the left and right 
                        // using the operator (token).
                        newExpr = leftExpr + " " + token + " " + rightExpr;

                        // Push the new intermediate expression on the stack
                        stack.Push(new Intermediate(newExpr, token));
                    }
                    else
                    {
                        if (Regex.IsMatch(token, @"[-0-9.]+"))
                        {
                            // NUMBER
                            stack.Push(new Intermediate(token, ""));
                        }
                        else
                        {
                            // OPERATOR
                            switch (token)
                            {
                                case "pi":
                                    stack.Push(new Intermediate("Math.PI", ""));
                                    break;
                                // JUST SKIP
                                case "dgrd": // DG2RAD
                                    if (stack.Count > 1)
                                    {
                                        var rightIntermediate = stack.Pop();
                                        var leftIntermediate = stack.Pop();
                                        newExpr = "( " + leftIntermediate.expr + rightIntermediate.expr + " ) * Math.PI/180";
                                    }
                                    else if (stack.Count > 0)
                                    {
                                        var rightIntermediate = stack.Pop();
                                        newExpr = "( " + rightIntermediate.expr + " ) * Math.PI/180";
                                    }
                                    else
                                    {
                                        newExpr = "Math.PI/180";
                                    }

                                    stack.Push(new Intermediate(newExpr, token));
                                    break;
                                default:
                                    break;
                                // FSX STACK COMMANDS
                                case "b":
                                case "c":
                                case "d":
                                case "p":
                                case "r":
                                    if (stack.Count > 0)
                                        stack.Pop();
                                    break;

                            }

                        }
                    }
                }

                foreach (var obj in stack)
                {
                    Console.WriteLine("\"" + obj.expr + "\" [" + obj.oper + "]");
                }
                // The loop above leaves the final expression on the top of the stack.
                if (stack.Count > 0)
                    return stack.Peek().expr;
                else
                    return "";
            } catch (Exception)
            {
                return "";
            }
        }
        // POSTFIX CONVERTER ENDS
    }
}
