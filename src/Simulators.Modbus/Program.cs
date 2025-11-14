using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.CommandLine;
using System.Timers;

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
                case 2050: registers[i] = 0; break; // Garment type: 0 (none)
                case 2060: registers[i] = 1; break; // Ready status: 1
                case 2069: registers[i] = 1; break; // OK status: 1
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
            if (coils.Length > 1050) {
                // Toggle every 100ms to simulate pulse
                coils[1050] = !coils[1050];
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
        int read = await stream.ReadAsync(buf.AsMemory(0, 7)); // MBAP header
        if (read == 0) break;
        if (read < 7) continue;

        ushort txId = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(0,2));
        ushort protoId = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(2,2));
        ushort len = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(4,2));
        byte unitId = buf[6];
        if (protoId != 0) continue;

        int pduLen = len - 1;
        read = await stream.ReadAsync(buf.AsMemory(7, pduLen));
        if (read < pduLen) break;

        byte fc = buf[7];
        if (fc == 0x03 && pduLen >= 5)
        {
            ushort start = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(8,2));
            ushort qty = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(10,2));
            qty = (ushort)Math.Min(qty, (ushort)50); // limit

            int byteCount = qty * 2;
            var resp = new byte[7 + 2 + byteCount];
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0,2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2,2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4,2), (ushort)(1 + 2 + byteCount));
            resp[6] = unitId;
            resp[7] = 0x03;
            resp[8] = (byte)byteCount;

            for (int i = 0; i < qty; i++)
            {
                ushort val = 0;
                int idx = start + i;
                if (idx >= 0 && idx < registers.Length) val = registers[idx];
                BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(9 + i*2, 2), val);
            }

            await stream.WriteAsync(resp);
        } else if (fc == 0x06 && pduLen == 5) // Write Single Register
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
                        exceptionCode = 0x02; // Illegal data address
                    }

                    if (!isValid)
                    {
                        // Exception response
                        var respException = new byte[9];
                        BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(0,2), txId);
                        BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(2,2), 0);
                        BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(4,2), 0x0003);
                        respException[6] = unitId;
                        respException[7] = (byte)(fc | 0x80);
                        respException[8] = exceptionCode;
                        await stream.WriteAsync(respException);
                        continue;
                    }

                    // Write to register
                    registers[address] = value;

                    // Response: MBAP + FC + Address + Value
                    var resp = new byte[7 + 5];
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0,2), txId);
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2,2), 0);
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4,2), 0x0005);
                    resp[6] = unitId;
                    resp[7] = 0x06;
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(8,2), address);
                    BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(10,2), value);

                    await stream.WriteAsync(resp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling FC=0x06 request: {ex.Message}");
                }
            } else if (fc == 0x10 && pduLen >= 7) // Write Multiple Registers
        {
            ushort start = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(8,2));
            ushort qty = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(10,2));
            byte byteCount = buf[12];

            // Validate request
            bool isValid = true;
            byte exceptionCode = 0x00;

            // Check if quantity is between 1 and 123 (Modbus limit for FC=10)
            if (qty < 1 || qty > 123)
            {
                isValid = false;
                exceptionCode = 0x03; // Illegal data value
            }
            // Check if byteCount matches quantity * 2
            else if (byteCount != qty * 2)
            {
                isValid = false;
                exceptionCode = 0x03; // Illegal data value
            }
            // Check if start + qty exceeds register array length
            else if (start + qty > registers.Length)
            {
                isValid = false;
                exceptionCode = 0x02; // Illegal data address
            }

            if (!isValid)
            {
                // Exception response
                var respException = new byte[9];
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(0,2), txId);
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(2,2), 0);
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(4,2), 0x0003);
                respException[6] = unitId;
                respException[7] = (byte)(fc | 0x80);
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
            var resp = new byte[7 + 5];
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0,2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2,2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4,2), 0x0005);
            resp[6] = unitId;
            resp[7] = 0x10;
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(8,2), start);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(10,2), qty);

            await stream.WriteAsync(resp);
        }
        else if (fc == 0x01 && pduLen >= 5) // Read Coils
        {
            ushort start = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(8, 2));
            ushort qty = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(10, 2));
            qty = (ushort)Math.Min(qty, (ushort)2000); // Modbus limit

            int byteCount = (qty + 7) / 8;
            // 修改响应数组长度为 7+2+byteCount
            var resp = new byte[7 + 2 + byteCount];
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0, 2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2, 2), 0);
            // 修改MBAP头长度字段为 3+byteCount
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4, 2), (ushort)(3 + byteCount));
            resp[6] = unitId;
            resp[7] = 0x01;
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
        else if (fc == 0x05 && pduLen == 5) // Write Single Coil
        {
            ushort coilAddr = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(8, 2));
            ushort coilValueRaw = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(10, 2));
            bool coilValue = coilValueRaw == 0xFF00;

            if (coilAddr >= coils.Length)
            {
                // Exception response: Illegal data address
                var respException = new byte[9];
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(0, 2), txId);
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(2, 2), 0);
                BinaryPrimitives.WriteUInt16BigEndian(respException.AsSpan(4, 2), 0x0003);
                respException[6] = unitId;
                respException[7] = (byte)(fc | 0x80);
                respException[8] = 0x02;
                await stream.WriteAsync(respException);
                continue;
            }

            coils[coilAddr] = coilValue;
            Console.WriteLine($"Coil {coilAddr} set to {coilValue}");

            // Response
            var resp = new byte[7 + 5];
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0, 2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2, 2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4, 2), 0x0005);
            resp[6] = unitId;
            resp[7] = 0x05;
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(8, 2), coilAddr);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(10, 2), coilValue ? (ushort)0xFF00 : (ushort)0x0000);
            await stream.WriteAsync(resp);
        }
        else
        {
            // Exception response: function code + 0x80, ILLEGAL FUNCTION
            var resp = new byte[9];
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(0,2), txId);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(2,2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(resp.AsSpan(4,2), 0x0003);
            resp[6] = unitId;
            resp[7] = (byte)(fc | 0x80);
            resp[8] = 0x01;
            await stream.WriteAsync(resp);
        }
    }
}
