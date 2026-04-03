namespace HouseholdBudgetMate.Application.Kernel.Exceptions;

public class BadRequestException(string message) : Exception(message);