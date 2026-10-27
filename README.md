# 🚀 Portfólio Profissional — Rafael Réus Mattos

Aplicação web completa para exibição de projetos, histórico de carreira e formulário de contato funcional, acompanhada de um **Painel Administrativo dinâmico** com autenticação JWT e banco de dados relacional MySQL.

Construído com **C# .NET 9**, **Blazor Server**, **Entity Framework Core** e **MySQL**.

---

## 🛠️ Tecnologias Utilizadas

- **Backend / Frontend:** .NET 9 (Blazor Web App / Interactive Server)
- **Banco de Dados:** MySQL 8.0 via Pomelo Entity Framework Core
- **Autenticação:** JWT (JSON Web Tokens) com validação de assinatura e expiração persistida via `ProtectedLocalStorage`
- **Upload de Mídias:** Armazenamento local com validação de assinatura binária (magic numbers) para proteção contra arquivos maliciosos
- **Estilização:** CSS moderno puro com tema dark, glassmorphism e animações fluidas
- **Containerização:** Docker e Docker Compose

---

## 📁 Estrutura do Projeto

```text
Portfolio/
├── .config/                  # Ferramentas locais (.NET tools - dotnet-ef)
├── src/
│   └── Portfolio.Web/        # Aplicação principal Blazor Web App
│       ├── Api/              # Endpoints REST para autenticação e consumo externo
│       ├── Auth/             # Serviços de JWT e AuthenticationStateProvider
│       ├── Components/       # Páginas Blazor, Layouts e Componentes Compartilhados
│       │   ├── Layout/       # MainLayout e AdminLayout (com rota protegida)
│       │   ├── Pages/        # Home, Detalhes de Projetos e Painel Admin
│       │   └── Shared/       # Cards, Formulário de Contato, Diálogos e Ícones SVG
│       ├── Data/             # Entidades (Project, Experience, Contact, Admin) e DbContext
│       ├── Migrations/       # Histórico de migrations do EF Core
│       ├── Services/         # Serviços de portfólio, e-mail/contato e upload de imagens
│       └── wwwroot/          # Arquivos estáticos (CSS, imagens e uploads)
├── docker-compose.yml        # Configuração do container MySQL 8
└── nuget.config              # Configuração de fontes do NuGet
```

---

## ⚡ Como Rodar Localmente

### Pré-requisitos
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) instalado
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) em execução (para o MySQL)

### 1. Iniciar o Banco de Dados MySQL
Na raiz do projeto, execute:
```powershell
docker compose up -d
```
*(O container subirá na porta padrão `3306` com as credenciais já configuradas para desenvolvimento).*

### 2. Executar a Aplicação
Execute o projeto com o modo de desenvolvimento:
```powershell
dotnet watch --project src/Portfolio.Web
```
*(Ou utilize `dotnet run --project src/Portfolio.Web`).*

Ao iniciar, a aplicação:
1. Conecta no MySQL e aplica as migrations automaticamente.
2. Cadastra os projetos e experiências iniciais.
3. Cria a conta padrão do administrador.

---

## 🌐 Acesso

- **Site Público:** [http://localhost:5171](http://localhost:5171)
- **Painel Administrativo:** [http://localhost:5171/admin](http://localhost:5171/admin)
  - **E-mail:** `admin@portfolio.local`
  - **Senha:** `Admin@123`

---

## 🔐 Variáveis de Ambiente para Produção

Ao realizar o deploy em ambiente produtivo, configure as seguintes variáveis no servidor de hospedagem para sobrescrever as credenciais de desenvolvimento:

| Variável de Ambiente | Descrição |
| :--- | :--- |
| `ConnectionStrings__DefaultConnection` | String de conexão do MySQL de produção |
| `Jwt__Key` | Chave secreta HMAC-SHA256 (mínimo de 32 caracteres) |
| `Admin__Email` | E-mail do administrador inicial |
| `Admin__Password` | Senha forte do administrador |
| `Smtp__Host` *(Opcional)* | Servidor SMTP para disparo de e-mails de contato |
| `Smtp__User` / `Smtp__Password` | Credenciais do serviço de envio de e-mail |

---

## 👨‍💻 Autor

**Rafael Réus Mattos**
- LinkedIn: [linkedin.com/in/rafael-r-mattos](https://www.linkedin.com/in/rafael-r-mattos)
- GitHub: [github.com/rrmattos](https://github.com/rrmattos)
