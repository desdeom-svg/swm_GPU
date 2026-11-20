using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class IntPadPatch
{
    private const string OriginalHash = "21FECEFF447D5A641BF6A4DC8FEB12C0C72DB46A6428B6C48F33960D723E1AF3";

    public static void Apply(string input, string output)
    {
        if (File.Exists(output))
            throw new IOException("Choose a new output file to preserve the original DLL.");

        byte[] original = File.ReadAllBytes(input);
        using (var sha256 = SHA256.Create())
        {
            string hash = BitConverter.ToString(sha256.ComputeHash(original)).Replace("-", "");
            if (hash != OriginalHash)
                throw new InvalidDataException("The SWM.dll SHA-256 does not match the inspected field build: " + hash);
        }

        using (var bytes = new MemoryStream(original))
        using (var assembly = AssemblyDefinition.ReadAssembly(bytes))
        {
            var module = assembly.MainModule;
            var parameters = module.GetType("SWM.Parameters");
            var getParam = Method(parameters, "GetParam");
            var getBrightness = (MethodReference)At(getParam, 0x00fb, Code.Callvirt).Operand;
            if (getBrightness.Name != "get_BrightBlackJudge" || getBrightness.ReturnType.MetadataType != MetadataType.Int32)
                throw new InvalidDataException("Unexpected BrightBlackJudge getter.");

            var padStride = AddPadStride(parameters);
            PatchPadRois(module, parameters);
            PatchImageArrayNum(parameters, getBrightness, padStride);
            PatchGetParam(getParam, getBrightness, padStride);
            using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write))
                assembly.Write(stream);
        }
    }

    private static MethodDefinition AddPadStride(TypeDefinition parameters)
    {
        var int32 = parameters.Module.TypeSystem.Int32;
        var method = new MethodDefinition("GetPadRecordSize",
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig, int32);
        method.Parameters.Add(new ParameterDefinition("brightBlackJudge", ParameterAttributes.None, int32));
        parameters.Methods.Add(method);
        var il = method.Body.GetILProcessor();
        var legacy = il.Create(OpCodes.Ldc_I4_5);

        // The field proxy uses the new core for modes 3, 4, and 5.
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_3);
        il.Emit(OpCodes.Sub);
        il.Emit(OpCodes.Ldc_I4_2);
        il.Emit(OpCodes.Bgt_Un, legacy);
        il.Emit(OpCodes.Ldc_I4_4);
        il.Emit(OpCodes.Ret);
        il.Append(legacy);
        il.Emit(OpCodes.Ret);
        return method;
    }

    private static void PatchPadRois(ModuleDefinition module, TypeDefinition parameters)
    {
        var method = Method(parameters, "get_PadRois");
        var il = method.Body.GetILProcessor();
        var rectangle = typeof(Rectangle);
        var width = module.ImportReference(rectangle.GetProperty("Width").GetGetMethod());
        var height = module.ImportReference(rectangle.GetProperty("Height").GetGetMethod());
        var floor = module.ImportReference(typeof(Math).GetMethod("Floor", new[] { typeof(double) }));
        var max = module.ImportReference(typeof(Math).GetMethod("Max", new[] { typeof(int), typeof(int) }));
        var fromLTRB = module.ImportReference(rectangle.GetMethod("FromLTRB",
            new[] { typeof(int), typeof(int), typeof(int), typeof(int) }));
        var intersect = module.ImportReference(rectangle.GetMethod("Intersect",
            new[] { rectangle, rectangle }));

        var rectangleLocal = method.Body.Variables[2];
        var dieBounds = new VariableDefinition(rectangleLocal.VariableType);
        method.Body.Variables.Add(dieBounds);
        var beforeEnumeration = At(method, 0x0007, Code.Nop);
        Insert(il, beforeEnumeration,
            il.Create(OpCodes.Ldc_I4_0),
            il.Create(OpCodes.Ldc_I4_0),
            il.Create(OpCodes.Ldc_I4_0),
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Call, Method(parameters, "get_dieWidth")),
            il.Create(OpCodes.Call, floor),
            il.Create(OpCodes.Conv_I4),
            il.Create(OpCodes.Call, max),
            il.Create(OpCodes.Ldc_I4_0),
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Call, Method(parameters, "get_dieHeight")),
            il.Create(OpCodes.Call, floor),
            il.Create(OpCodes.Conv_I4),
            il.Create(OpCodes.Call, max),
            il.Create(OpCodes.Call, fromLTRB),
            il.Create(OpCodes.Stloc, dieBounds));

        var originalTest = At(method, 0x0027, Code.Nop);
        var add = At(method, 0x0074, Code.Ldloc_0);
        var next = At(method, 0x007d, Code.Ldloca_S);
        Insert(il, originalTest,
            il.Create(OpCodes.Ldloca, rectangleLocal),
            il.Create(OpCodes.Call, width),
            il.Create(OpCodes.Ldc_I4_0),
            il.Create(OpCodes.Ble, next),
            il.Create(OpCodes.Ldloca, rectangleLocal),
            il.Create(OpCodes.Call, height),
            il.Create(OpCodes.Ldc_I4_0),
            il.Create(OpCodes.Ble, next),
            il.Create(OpCodes.Ldloc, rectangleLocal),
            il.Create(OpCodes.Ldloc, dieBounds),
            il.Create(OpCodes.Call, intersect),
            il.Create(OpCodes.Stloc, rectangleLocal),
            il.Create(OpCodes.Ldloca, rectangleLocal),
            il.Create(OpCodes.Call, width),
            il.Create(OpCodes.Ldc_I4_0),
            il.Create(OpCodes.Ble, next),
            il.Create(OpCodes.Ldloca, rectangleLocal),
            il.Create(OpCodes.Call, height),
            il.Create(OpCodes.Ldc_I4_0),
            il.Create(OpCodes.Ble, next));

        var obsolete = method.Body.Instructions.SkipWhile(i => i != originalTest).TakeWhile(i => i != add).ToArray();
        foreach (var instruction in obsolete)
            il.Remove(instruction);
        ExpandBranches(method);
    }

    private static void PatchImageArrayNum(TypeDefinition parameters, MethodReference getBrightness, MethodDefinition padStride)
    {
        var method = Method(parameters, "ImageArrayNum");
        var il = method.Body.GetILProcessor();
        var constant = At(method, 0x0089, Code.Ldc_I4_4);
        // Keep the original instruction as the first instruction of the replacement.
        constant.OpCode = OpCodes.Ldarg_0;
        Insert(il, constant.Next,
            il.Create(OpCodes.Call, Method(parameters, "get_inspectionParameter")),
            il.Create(OpCodes.Callvirt, getBrightness),
            il.Create(OpCodes.Call, padStride));
        ExpandBranches(method);
    }

    private static void PatchGetParam(MethodDefinition method, MethodReference getBrightness, MethodDefinition padStride)
    {
        var il = method.Body.GetILProcessor();
        var stride = new VariableDefinition(method.Module.TypeSystem.Int32);
        method.Body.Variables.Add(stride);
        var constant = At(method, 0x01b4, Code.Ldc_I4_4);
        constant.OpCode = OpCodes.Ldloc;
        constant.Operand = method.Body.Variables[13]; // WSTICParameters for this slice.
        Insert(il, constant.Next,
            il.Create(OpCodes.Callvirt, getBrightness),
            il.Create(OpCodes.Call, padStride),
            il.Create(OpCodes.Dup),
            il.Create(OpCodes.Stloc, stride));

        foreach (int offset in new[] { 0x059c, 0x05c4, 0x05ec, 0x0614 })
        {
            var step = At(method, offset, Code.Ldc_I4_5);
            step.OpCode = OpCodes.Ldloc;
            step.Operand = stride;
        }

        // The complete mode-dependent legacy flag switch stays in the 5-double route.
        var flagSwitch = At(method, 0x062a, Code.Ldloc_S);
        var nextPad = At(method, 0x06fb, Code.Nop);
        Insert(il, flagSwitch,
            il.Create(OpCodes.Ldloc, stride),
            il.Create(OpCodes.Ldc_I4_4),
            il.Create(OpCodes.Beq, nextPad));
        ExpandBranches(method);
    }

    private static void Insert(ILProcessor il, Instruction before, params Instruction[] instructions)
    {
        foreach (var instruction in instructions)
            il.InsertBefore(before, instruction);
    }

    private static MethodDefinition Method(TypeDefinition type, string name)
    {
        return type.Methods.Single(method => method.Name == name);
    }

    private static Instruction At(MethodDefinition method, int offset, Code expected)
    {
        var instructions = method.Body.Instructions;
        var instruction = instructions.Single(i => i.Offset == offset);
        if (instruction.OpCode.Code != expected)
            throw new InvalidDataException(String.Format("{0}: unexpected IL at 0x{1:x}.", method.Name, offset));
        return instruction;
    }

    private static void ExpandBranches(MethodDefinition method)
    {
        // Insertions can lengthen branches that were within the original short range.
        foreach (var instruction in method.Body.Instructions)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Br_S: instruction.OpCode = OpCodes.Br; break;
                case Code.Brfalse_S: instruction.OpCode = OpCodes.Brfalse; break;
                case Code.Brtrue_S: instruction.OpCode = OpCodes.Brtrue; break;
                case Code.Beq_S: instruction.OpCode = OpCodes.Beq; break;
                case Code.Bne_Un_S: instruction.OpCode = OpCodes.Bne_Un; break;
                case Code.Bge_S: instruction.OpCode = OpCodes.Bge; break;
                case Code.Bge_Un_S: instruction.OpCode = OpCodes.Bge_Un; break;
                case Code.Bgt_S: instruction.OpCode = OpCodes.Bgt; break;
                case Code.Bgt_Un_S: instruction.OpCode = OpCodes.Bgt_Un; break;
                case Code.Ble_S: instruction.OpCode = OpCodes.Ble; break;
                case Code.Ble_Un_S: instruction.OpCode = OpCodes.Ble_Un; break;
                case Code.Blt_S: instruction.OpCode = OpCodes.Blt; break;
                case Code.Blt_Un_S: instruction.OpCode = OpCodes.Blt_Un; break;
                case Code.Leave_S: instruction.OpCode = OpCodes.Leave; break;
            }
        }
    }
}
