// Copyright 2024 by PeopleWare n.v..
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Security.Claims;

using NUnit.Framework;

using PPWCode.Util.Authorization.I;
using PPWCode.Util.Time.I;

namespace PPWCode.Vernacular.Persistence.V.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Fixtures)]
public class InMemoryPersonRepositoryTests : BaseFixture
{
    protected List<Person>? BaseModels { get; private set; }

    /// <inheritdoc />
    protected override void OnSetup()
    {
        base.OnSetup();

        BaseModels = [];
    }

    /// <inheritdoc />
    protected override void OnTeardown()
    {
        BaseModels?.Clear();

        base.OnTeardown();
    }

    protected InMemoryPersonRepository CreateRepository(IEnumerable<Person> people, DateTimeOffset? now = null, string? userName = null)
    {
        ITimeProvider<DateTimeOffset> timeProvider = new TimeProvider(() => now ?? DateTimeOffset.Now);
        ClaimsIdentity identity = new ("MyAuthentication", ClaimTypes.Name, null);
        identity.AddClaim(new Claim(ClaimTypes.Name, userName ?? "SomeOne"));
        identity.AddClaim(new Claim("SUT", "testing"));
        ClaimsPrincipal principal = new (identity);
        IPrincipalProvider principalProvider = new PrincipalProvider(principal);
        IIdentityProvider identityProvider = new IdentityProvider(principalProvider);
        PersonQueryManager queryManager = new ();

        InMemoryPersonRepository repository = new (BaseModels!, timeProvider, identityProvider, queryManager);
        repository.Initialize(people);

        return repository;
    }

    [Test]
    public async Task can_GetByIdAsync()
    {
        // Arrange
        long expectedId = IdGenerator.NextId;
        Person specificPerson = new () { Id = expectedId, Name = "Jef" };
        IEnumerable<Person> people = [specificPerson];
        InMemoryPersonRepository repository = CreateRepository(people);

        // Act
        Person? person = await repository.GetByIdAsync(expectedId);

        // Assert
        Assert.That(person, Is.Not.Null);
        Assert.That(person, Is.EqualTo(specificPerson));
    }

    [Test]
    public void can_GetById()
    {
        // Arrange
        long expectedId = IdGenerator.NextId;
        Person specificPerson = new () { Id = expectedId, Name = "Jef" };
        IEnumerable<Person> people = [specificPerson];
        InMemoryPersonRepository repository = CreateRepository(people);

        // Act
        Person? person = repository.GetById(expectedId);

        // Assert
        Assert.That(person, Is.Not.Null);
        Assert.That(person, Is.EqualTo(specificPerson));
    }

    [Test]
    public async Task can_FindAllAsync()
    {
        // Arrange
        Person specificPerson = new () { Id = 1L, Name = "Jef" };
        IEnumerable<Person> people = [specificPerson];
        InMemoryPersonRepository repository = CreateRepository(people);

        // Act
        List<Person> foundPeople = await repository.FindAllAsync();

        // Assert
        Assert.That(foundPeople, Is.EquivalentTo(people));
    }

    [Test]
    public void can_FindAll()
    {
        // Arrange
        long expectedId = IdGenerator.NextId;
        Person specificPerson = new () { Id = expectedId, Name = "Jef" };
        IEnumerable<Person> people = [specificPerson];
        InMemoryPersonRepository repository = CreateRepository(people);

        // Act
        List<Person> foundPeople = repository.FindAll();

        // Assert
        Assert.That(foundPeople, Is.EquivalentTo(people));
    }

    [Test]
    public async Task can_UpdateAsync()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.Now;
        string userName = Guid.NewGuid().ToString();
        string newName = Guid.NewGuid().ToString();
        Person specificPerson = new () { Id = IdGenerator.NextId, Name = "Jef" };
        IEnumerable<Person> people = [specificPerson];
        InMemoryPersonRepository repository = CreateRepository(people, now, userName);

        // Act
        specificPerson.Name = newName;
        Assert.That(specificPerson.LastModifiedAt, Is.Null);
        Assert.That(specificPerson.LastModifiedBy, Is.Null);
        await repository.UpdateAsync(specificPerson);

        // Assert
        Assert.That(specificPerson.LastModifiedAt, Is.EqualTo(now));
        Assert.That(specificPerson.LastModifiedBy, Is.EqualTo(userName));
    }

    [Test]
    public void can_Update()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.Now;
        string userName = Guid.NewGuid().ToString();
        string newName = Guid.NewGuid().ToString();
        Person specificPerson = new () { Id = IdGenerator.NextId, Name = "Jef" };
        IEnumerable<Person> people = [specificPerson];
        InMemoryPersonRepository repository = CreateRepository(people, now, userName);

        // Act
        specificPerson.Name = newName;
        Assert.That(specificPerson.LastModifiedAt, Is.Null);
        Assert.That(specificPerson.LastModifiedBy, Is.Null);
        repository.Update(specificPerson);

        // Assert
        Assert.That(specificPerson.LastModifiedAt, Is.EqualTo(now));
        Assert.That(specificPerson.LastModifiedBy, Is.EqualTo(userName));
    }

    [Test]
    public async Task can_InsertAsync()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.Now;
        string userName = Guid.NewGuid().ToString();
        InMemoryPersonRepository repository = CreateRepository([], now, userName);

        // Act
        Assert.That(repository.Models, Is.Empty);
        Person person = new () { Name = "Jef" };
        await repository.InsertAsync(person);

        // Assert
        Assert.That(repository.Models, Is.Not.Empty);
        Assert.That(repository.Models, Has.Count.EqualTo(1));
        Assert.That(repository.Models[0], Is.EqualTo(person));
        Assert.That(person.CreatedAt, Is.EqualTo(now));
        Assert.That(person.CreatedBy, Is.EqualTo(userName));
    }

    [Test]
    public void can_Insert()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.Now;
        string userName = Guid.NewGuid().ToString();
        InMemoryPersonRepository repository = CreateRepository([], now, userName);

        // Act
        Assert.That(repository.Models, Is.Empty);
        Person person = new () { Name = "Jef" };
        repository.Insert(person);

        // Assert
        Assert.That(repository.Models, Is.Not.Empty);
        Assert.That(repository.Models, Has.Count.EqualTo(1));
        Assert.That(repository.Models[0], Is.EqualTo(person));
        Assert.That(person.CreatedAt, Is.EqualTo(now));
        Assert.That(person.CreatedBy, Is.EqualTo(userName));
    }

    [Test]
    public async Task can_DeleteAsync()
    {
        // Arrange
        int id = IdGenerator.NextId;
        Person person = new () { Id = id, Name = "Jef" };
        IEnumerable<Person> people = [person];
        InMemoryPersonRepository repository = CreateRepository(people);

        // Act
        Assert.That(repository.Models, Has.Member(person));
        await repository.DeleteAsync(person);

        // Assert
        Assert.That(repository.Models, Has.No.Member(person));
    }

    [Test]
    public void can_Delete()
    {
        // Arrange
        int id = IdGenerator.NextId;
        Person person = new () { Id = id, Name = "Jef" };
        IEnumerable<Person> people = [person];
        InMemoryPersonRepository repository = CreateRepository(people);

        // Act
        Assert.That(repository.Models, Has.Member(person));
        repository.Delete(person);

        // Assert
        Assert.That(repository.Models, Has.No.Member(person));
    }

    [Test]
    public async Task can_FindByNameAsync()
    {
        // Arrange
        int id = IdGenerator.NextId;
        string name = "Jef";
        Person person = new () { Id = id, Name = name };
        Person person2 = new () { Id = id, Name = "2" + name };
        Person person3 = new () { Id = id, Name = "3" + name };
        IEnumerable<Person> people = [person, person2, person3];
        InMemoryPersonRepository repository = CreateRepository(people);

        // Act
        List<Person> foundPeople = await repository.FindByNameAsync(name);

        // Assert
        Assert.That(foundPeople, Is.Not.Empty);
        Assert.That(foundPeople, Has.Count.EqualTo(1));
        Assert.That(foundPeople[0], Is.EqualTo(person));
    }
}
