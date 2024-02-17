using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var app = builder.Build();

app.UseHttpsRedirection();

app.MapPost("/clientes/{id}/transacoes", async (int id, [FromBody] Transacao transacao) =>
{
    if (id == 0)
    {
        return Results.NotFound();
    }

    var cliente = new Cliente
    {
        Limite = 100000,
        Saldo = -9098
    };

    if (transacao.Tipo == "d")
    {
        if (cliente.Saldo < cliente.Limite)
        {
            return Results.StatusCode(422);
        }
    }

    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    await using (var cmd = new NpgsqlCommand("INSERT INTO transacoes (cliente_id, valor, tipo, descricao, realizada_em) VALUES (@cliente_id, @valor, @tipo, @descricao, @realizada_em)", conn))
    {
        cmd.Parameters.AddWithValue("cliente_id", id);
        cmd.Parameters.AddWithValue("valor", transacao.Valor);
        cmd.Parameters.AddWithValue("tipo", transacao.Tipo);
        cmd.Parameters.AddWithValue("descricao", transacao.Descricao);
        cmd.Parameters.AddWithValue("realizada_em", DateTime.Now);

        var result = await cmd.ExecuteNonQueryAsync();

        if (result == 1)
        {
            return Results.Ok(cliente);
        }
        else
        {
            return Results.Problem("Não foi possível criar a transação");
        }
    }
});

app.MapGet("/clientes/{id}/extrato", async (int id) =>
{
    if (id == 0)
    {
        return Results.NotFound();
    }

    var transacoes = new List<Transacao>();

    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    await using (var cmd = new NpgsqlCommand("SELECT valor, tipo, descricao, realizada_em FROM transacoes WHERE cliente_id = @cliente_id ORDER BY realizada_em DESC LIMIT 10", conn))
    {
        cmd.Parameters.AddWithValue("cliente_id", id);

        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                transacoes.Add(new Transacao
                {
                    Valor = (int)reader["valor"],
                    Tipo = reader["tipo"].ToString(),
                    Descricao = reader["descricao"].ToString(),
                    RealizadaEm = (DateTime)reader["realizada_em"]
                });
            }
        }
    }

    var extrato = new Extrato
    {
        Saldo = new Saldo
        {
            Total = -1, // Aqui você deve calcular o saldo atual com base nas transações
            DataExtrato = DateTime.Now,
            Limite = 100000 // Defina o limite conforme necessário
        },
        UltimasTransacoes = transacoes
    };

    return Results.Ok(extrato);
});

app.Run();

internal record Transacao
{
    /// <summary>
    /// 1000 = R$10,00
    /// </summary>
    public int Valor { get; set; }
    
    /// <summary>
    /// c = crédito
    /// d = débito
    /// </summary>
    public string Tipo { get;set;}
    
    [Length(1, 10)]
    public string Descricao { get; set; }

    [JsonPropertyName("realizada_em")]
    public DateTime RealizadaEm { get; set; }
}

internal record Cliente
{
    public int Limite { get; set; }

    public int Saldo { get; set; }
}

internal record Extrato
{
    public Saldo Saldo { get; set; }

    [JsonPropertyName("ultimas_transacoes")]
    public List<Transacao> UltimasTransacoes { get; set; }
}

internal record Saldo
{
    public int Total { get; set; }
    
    [JsonPropertyName("data_extrato")]
    public DateTime DataExtrato { get; set; }
    public int Limite { get; set; }
}