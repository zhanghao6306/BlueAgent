using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.CommandLine;
using System.Timers;

// Modbus Function Codes
public static class ModbusFunctionCodes
{
    public const byte ReadCoils = 0x01;
    public const byte ReadHoldingRegisters = 0x03;
    public const byte WriteSingleCoil = 0x05;
    public const byte WriteSingleRegister = 0x06;
    public const byte WriteMultipleRegisters = 0x10;
}

// Modbus Exception Codes
public static class ModbusExceptionCodes
{
    public const byte IllegalFunction = 0x01;
    public const byte IllegalDataAddress = 0x02;
    public const byte IllegalDataValue = 0x03;
}

// Modbus Constants
public static class ModbusConstants
{
    public const ushort MaxReadCoils = 2000;
    public const ushort MaxReadRegisters = 50;
    public const ushort MaxWriteRegisters = 123;
    public const ushort CoilOnValue = 0xFF00;
    public const ushort CoilOffValue = 0x0000;
    public static byte ExceptionFunctionCode(byte fc) => (byte)(fc | 0x80);
}

// PLC Register Addresses (D registers)
public static class PlcRegisters
{
    public const ushort StartHandshake = 2000;
    public const ushort ClientFeedbackAck = 2005;
    public const ushort FoundationPrintComplete = 2001;
    public const ushort WhitePrintComplete = 2002;
    public const ushort ColorPrintComplete = 2003;
    public const ushort TopcoatPrintComplete = 2004;
    public const ushort PlatformSoftStart = 2006;
    public const ushort PlatformSoftStop = 2007;
    public const ushort GarmentType = 2050;
    public const ushort ReadyStatus = 2060;
    public const ushort PlatformStatus = 2068;
    public const ushort OkStatus = 2069;
}

// PLC Coil Addresses (M registers)
public static class PlcCoils
{
    public const ushort StartHandshakeAck = 1000;
    public const ushort ClientFeedbackAck = 1015;
    public const ushort FoundationPrintCompleteAck = 1001;
    public const ushort WhitePrintCompleteAck = 1002;
    public const ushort ColorPrintCompleteAck = 1003;
    public const ushort TopcoatPrintCompleteAck = 1004;
    public const ushort PlatformSoftStartAck = 1013;
    public const ushort PlatformSoftStopAck = 1014;
    public const ushort PlatformStatusAck = 1068;
    public const ushort PlatformRotation = 1050;
}

// MBAP Constants
public static class MbapConstants
{
    public const int HeaderLength = 7;
    public const ushort ProtocolId = 0;
    public const ushort ExceptionResponseLength = 0x0003;
    public const ushort NormalResponseLength = 0x0005;
}

namespace Simulators.Modbus
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Parse command line arguments
            var portOption = new Option<int>("--port", () => 5020, "Modbus TCP port");
            var sizeOption = new Option<int>("--size", () => 2100, "Number of holding registers (should be at least 2100 to cover all defined addresses)");
            var seedOption = new Option<int?> ("--seed", () => null, "Random seed for register data");

            var rootCommand = new RootCommand("Modbus TCP Simulator");
            rootCommand.AddOption(portOption);
            rootCommand.AddOption(sizeOption);
            rootCommand.AddOption(seedOption);

            rootCommand.SetHandler(async (port, size, seed) => {
                await RunSimulator(port, size, seed);
            }, portOption, sizeOption, seedOption);

            await rootCommand.InvokeAsync(args);
        }

        static async Task RunSimulator(int port, int size, int? seed) {
    // Validate register size
    if (size < 1 || size > 65536) {
        Console.WriteLine("Error: Register size must be between 1 and 65536");
        return;
    }

    Console.WriteLine($"Modbus TCP Simulator listening on 0.0.0.0:{port} (FC=0x01, 0x03, 0x05, 0x10)");
    var listener = new TcpListener(IPAddress.Any, port);
    listener.Start();

    // Initialize registers and coils
    var registers = new ushort[size];
    var coils = new bool[size]; // Coils share same address space for simplicity
    
    // Initialize register data
        if (seed.HasValue) {
            var rand = new Random(seed.Value);
            for (int i = 0; i < registers.Length; i++) {
                registers[i] = (ushort)rand.Next(ushort.MaxValue + 1);
            }
            for (int i = 0; i < coils.Length; i++) {
                coils[i] = rand.Next(2) == 1;
            }
        } else {
            // Initialize registers with demo data
            for (int i = 0; i < registers.Length; i++) {
                // Set default values for specific registers
                switch (i) {
                    case PlcRegisters.GarmentType: registers[i] = 0; break; // Garment type: 0 (none)
                    case PlcRegisters.ReadyStatus: registers[i] = 1; break; // Ready status: 1
                    case PlcRegisters.OkStatus: registers[i] = 1; break; // OK status: 1
                    default: registers[i] = (ushort)(i * 10); break; // demo data for other registers
                }
            }
            Array.Fill(coils, false); // Initialize all coils to false
        }

    // Initialize PLC state machine
    var stateMachine = new PlcStateMachine(registers, coils);

    // Start state machine task
    _ = Task.Run(async () => {
        while (true) {
            stateMachine.Update();
            await Task.Delay(100); // Main loop delay
        }
    });

    // Start platform rotation simulation (M1050)
    _ = Task.Run(async () => {
        while (true) {
            if (coils.Length > PlcCoils.PlatformRotation) {
                // Toggle every 100ms to simulate pulse
                coils[PlcCoils.PlatformRotation] = !coils[PlcCoils.PlatformRotation];
            }
            await Task.Delay(100);
        }
    });

    // Start accepting clients
    _ = Task.Run(async () => {
        while (true) {
            var client = await listener.AcceptTcpClientAsync();
            _ = HandleClient(client, registers, coils);
        }
    });

    await Task.Delay(-1);
}

static async Task HandleClient(TcpClient client, ushort[] registers, bool[] coils)
{
    using var c = client;
    var stream = c.GetStream();
    var buf = new byte[260];

    while (true)
    {
        int read = await stream.ReadAsync(buf.AsMemory(0, MbapConstants.HeaderLength)); // MBAP header
        if (read == 0) break;
        if (read < MbapConstants.HeaderLength) continue;

        ushort txId = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(0,2));
        ushort protoId = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(2,2));
        ushort len = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(4,2));
        byte unitId = buf[6];
        if (protoId != MbapConstants.ProtocolId) continue;

        int pduLen = len - 1;
        read = await stream.ReadAsync(buf.AsMemory(7, pduLen));
        if (read < pduLen) break;

        byte fc = buf[7];
        if (fc == ModbusFunctionCodes.ReadHoldingRegisters && pduLen >= 5)
        {
            ushort start = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(8,2));
            ushort qty = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(10,2));
            qty = (ushort)Math.Min(qty, ModbusConstants.MaxReadRegisters); // limit

            int byteCount = qty * 2;
            var resp = new byte[MbapConstants.HeaderLength + 2 + byteCount];
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0,2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2,2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4,2), (ushort)(1 + 2 + byteCount));
            resp[6] = unitId;
            resp[7] = ModbusFunctionCodes.ReadHoldingRegisters;
            resp[8] = (byte)byteCount;

            for (int i = 0; i < qty; i++)
            {
                ushort val = 0;
                int idx = start + i;
                if (idx >= 0 && idx < registers.Length) val = registers[idx];
                BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(9 + i*2, 2), val);
            }

            await stream.WriteAsync(resp);
        } else if (fc == ModbusFunctionCodes.WriteSingleRegister && pduLen == 5) // Write Single Register
            {
                try
                {
                    ushort address = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(8,2));
                    ushort value = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(10,2));

                    // Validate request
                    bool isValid = true;
                    byte exceptionCode = 0x00;

                    if (address >= registers.Length)
                    {
                        isValid = false;
                        exceptionCode = ModbusExceptionCodes.IllegalDataAddress; // Illegal data address
                    }

                    if (!isValid)
                    {
                        // Exception response
                        var respException = new byte[9];
                        BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(0,2), txId);
                        BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(2,2), 0);
                        BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(4,2), MbapConstants.ExceptionResponseLength);
                        respException[6] = unitId;
                        respException[7] = ModbusConstants.ExceptionFunctionCode(fc);
                        respException[8] = exceptionCode;
                        await stream.WriteAsync(respException);
                        continue;
                    }

                    // Write to register
                    registers[address] = value;

                    // Response: MBAP + FC + Address + Value
                    var resp = new byte[MbapConstants.HeaderLength + 5];
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0,2), txId);
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2,2), 0);
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4,2), MbapConstants.NormalResponseLength);
                    resp[6] = unitId;
                    resp[7] = ModbusFunctionCodes.WriteSingleRegister;
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(8,2), address);
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(10,2), value);

                    await stream.WriteAsync(resp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling FC=0x06 request: {ex.Message}");
                }
            } else if (fc == ModbusFunctionCodes.WriteMultipleRegisters && pduLen >= 7) // Write Multiple Registers
        {
            ushort start = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(8,2));
            ushort qty = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(10,2));
            byte byteCount = buf[12];

            // Validate request
            bool isValid = true;
            byte exceptionCode = 0x00;

            // Check if quantity is between 1 and 123 (Modbus limit for FC=10)
            if (qty < 1 || qty > ModbusConstants.MaxWriteRegisters)
            {
                isValid = false;
                exceptionCode = ModbusExceptionCodes.IllegalDataValue; // Illegal data value
            }
            // Check if byteCount matches quantity * 2
            else if (byteCount != qty * 2)
            {
                isValid = false;
                exceptionCode = ModbusExceptionCodes.IllegalDataValue; // Illegal data value
            }
            // Check if start + qty exceeds register array length
            else if (start + qty > registers.Length)
            {
                isValid = false;
                exceptionCode = ModbusExceptionCodes.IllegalDataAddress; // Illegal data address
            }

            if (!isValid)
            {
                // Exception response
                var respException = new byte[9];
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(0,2), txId);
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(2,2), 0);
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(4,2), MbapConstants.ExceptionResponseLength);
                respException[6] = unitId;
                respException[7] = ModbusConstants.ExceptionFunctionCode(fc);
                respException[8] = exceptionCode;
                await stream.WriteAsync(respException);
                continue;
            }

            // Write data to registers
            for (int i = 0; i < qty; i++)
            {
                int idx = start + i;
                ushort val = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(13 + i*2, 2));
                registers[idx] = val;
            }

            // Response: MBAP + FC + Start Address + Quantity
            var resp = new byte[MbapConstants.HeaderLength + 5];
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0,2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2,2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4,2), MbapConstants.NormalResponseLength);
            resp[6] = unitId;
            resp[7] = ModbusFunctionCodes.WriteMultipleRegisters;
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(8,2), start);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(10,2), qty);

            await stream.WriteAsync(resp);
        }
        else if (fc == ModbusFunctionCodes.ReadCoils && pduLen >= 5) // Read Coils
        {
            ushort start = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(8, 2));
            ushort qty = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(10, 2));
            qty = (ushort)Math.Min(qty, ModbusConstants.MaxReadCoils); // Modbus limit

            int byteCount = (qty + 7) / 8;
            // 修改响应数组长度为 MbapConstants.HeaderLength + 2 + byteCount
            var resp = new byte[7 + 2 + byteCount];
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0, 2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2, 2), 0);
            // 修改MBAP头长度字段为 1 + 2 + byteCount (UnitID + FC + ByteCount + Data)
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4, 2), (ushort)(1 + 2 + byteCount));
            resp[6] = unitId;
            resp[7] = ModbusFunctionCodes.ReadCoils;
            resp[8] = (byte)byteCount;

            for (int i = 0; i < qty; i++)
            {
                int coilIdx = start + i;
                bool coilValue = coilIdx >= 0 && coilIdx < coils.Length ? coils[coilIdx] : false;
                int bytePos = 9 + (i / 8);
                int bitPos = i % 8;
                if (coilValue)
                {
                    resp[bytePos] |= (byte)(1 << bitPos);
                }
            }

            await stream.WriteAsync(resp);
        }
        else if (fc == ModbusFunctionCodes.WriteSingleCoil && pduLen == 5) // Write Single Coil
        {
            ushort coilAddr = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(8, 2));
            ushort coilValueRaw = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(10, 2));
            bool coilValue = coilValueRaw == ModbusConstants.CoilOnValue;

            if (coilAddr >= coils.Length)
            {
                // Exception response: Illegal data address
                var respException = new byte[9];
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(0, 2), txId);
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(2, 2), 0);
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(4, 2), MbapConstants.ExceptionResponseLength);
                respException[6] = unitId;
                respException[7] = ModbusConstants.ExceptionFunctionCode(fc);
                respException[8] = ModbusExceptionCodes.IllegalDataAddress;
                await stream.WriteAsync(respException);
                continue;
            }

            coils[coilAddr] = coilValue;
            Console.WriteLine($"Coil {coilAddr} set to {coilValue}");

            // Response
            var resp = new byte[7 + 5];
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0, 2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2, 2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4, 2), MbapConstants.NormalResponseLength);
            resp[6] = unitId;
            resp[7] = ModbusFunctionCodes.WriteSingleCoil;
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(8, 2), coilAddr);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(10, 2), coilValue ? ModbusConstants.CoilOnValue : ModbusConstants.CoilOffValue);
            await stream.WriteAsync(resp);
        }
        else
        {
            // Exception response: function code + 0x80, ILLEGAL FUNCTION
            var resp = new byte[9];
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0,2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2,2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4,2), MbapConstants.ExceptionResponseLength);
            resp[6] = unitId;
            resp[7] = ModbusConstants.ExceptionFunctionCode(fc);
            resp[8] = ModbusExceptionCodes.IllegalFunction;
            await stream.WriteAsync(resp);
        }
    }
}
    }
}
