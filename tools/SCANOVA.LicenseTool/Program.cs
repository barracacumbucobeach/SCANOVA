using System.Security.Cryptography;
using System.Text.Json;
using SCANOVA.LicenseTool;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

switch (args[0])
{
    case "gerar-chaves":
        return GerarChaves(args);
    case "emitir":
        return Emitir(args);
    default:
        PrintUsage();
        return 1;
}

static void PrintUsage()
{
    Console.WriteLine("""
        Ferramenta interna de licenciamento do SCANOVA — NÃO faz parte do aplicativo distribuído.

        Uso:
          gerar-chaves <pasta-destino>
              Gera um novo par de chaves ECDSA P-256. Grava a chave PRIVADA em
              <pasta-destino>/scanova-license-private-key.pem (guarde em local seguro, NUNCA
              neste repositório) e imprime a chave PÚBLICA (Base64) para colar em
              LicenseSigningPublicKey.cs.

          emitir <chave-privada.pem> <licenseId> <edition> [--cliente "Nome"] [--limite N]
              Emite uma nova chave de licença assinada e imprime a string final (o que o
              cliente cola no aplicativo). <licenseId> deve ser único por licença emitida
              (necessário para revogação individual — ver RevokedLicenses.json).
        """);
}

static int GerarChaves(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Uso: gerar-chaves <pasta-destino>");
        return 1;
    }

    var destino = args[1];
    Directory.CreateDirectory(destino);

    using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    var privateKeyPath = Path.Combine(destino, "scanova-license-private-key.pem");
    File.WriteAllText(privateKeyPath, ecdsa.ExportPkcs8PrivateKeyPem() + Environment.NewLine);

    var publicKeyBase64 = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());

    Console.WriteLine($"Chave privada gravada em: {privateKeyPath}");
    Console.WriteLine();
    Console.WriteLine("!! GUARDE ESSE ARQUIVO EM LOCAL SEGURO (ex.: gerenciador de senhas/cofre) !!");
    Console.WriteLine("!! NUNCA adicione esse arquivo ao controle de versão.                     !!");
    Console.WriteLine();
    Console.WriteLine("Chave pública (cole em LicenseSigningPublicKey.cs, campo PublicKeyBase64):");
    Console.WriteLine(publicKeyBase64);

    return 0;
}

static int Emitir(string[] args)
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Uso: emitir <chave-privada.pem> <licenseId> <edition> [--cliente \"Nome\"] [--limite N]");
        return 1;
    }

    var privateKeyPath = args[1];
    var licenseId = args[2];
    var edition = args[3];
    string? cliente = null;
    var limite = 1;

    for (var i = 4; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--cliente" when i + 1 < args.Length:
                cliente = args[++i];
                break;
            case "--limite" when i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedLimite):
                limite = parsedLimite;
                i++;
                break;
        }
    }

    if (!File.Exists(privateKeyPath))
    {
        Console.Error.WriteLine($"Arquivo de chave privada não encontrado: {privateKeyPath}");
        return 1;
    }

    using var ecdsa = ECDsa.Create();
    ecdsa.ImportFromPem(File.ReadAllText(privateKeyPath));

    var payload = new LicensePayload
    {
        LicenseId = licenseId,
        Product = "SCANOVA",
        Edition = edition,
        CustomerName = cliente,
        ActivationLimit = limite,
        CreatedAt = DateTime.UtcNow,
    };

    var payloadJson = JsonSerializer.SerializeToUtf8Bytes(payload);
    var signature = ecdsa.SignData(payloadJson, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    var licenseKey = LicenseEnvelope.Encode(payloadJson, signature);

    Console.WriteLine("Chave de licença emitida:");
    Console.WriteLine();
    Console.WriteLine(licenseKey);

    return 0;
}
