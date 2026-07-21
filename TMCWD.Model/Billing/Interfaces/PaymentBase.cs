using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;
using System.Net.NetworkInformation;
using TMCWD.Model.Administrator;

namespace TMCWD.Model.Billing.Interfaces;

public abstract class PaymentBase
{
    
    #region fields

    private readonly PaymentMethod _method;
    private readonly string _macAddress;
    private readonly string _paymentReferenceId;
    private readonly string _billingReferenceId;
    private readonly decimal _billingAmount;
    private readonly decimal _totalPayments;

    protected Billing _billing;
    protected readonly AdvancePayment _advancePayment;

    #endregion

    #region constructors

    public PaymentBase(Billing bill) 
    {
        _method = new PaymentMethod();
        _macAddress = GetPhysicalAddress();
        _paymentReferenceId = Guid.NewGuid().ToString().Replace("-", "").ToUpper();
        _billing = bill;
        _totalPayments = bill.TotalPaidAmount;
        _billingReferenceId = bill.BillingReferenceId;
        _advancePayment = bill.AdvancePayment;
    }

    public PaymentBase(Billing bill, PaymentMethod channel) 
    { 
        _method = channel;
        _macAddress = GetPhysicalAddress();
        _paymentReferenceId = Guid.NewGuid().ToString().Replace("-", "").ToUpper();
        _billing = bill;
        _totalPayments = bill.TotalPaidAmount;
        _billingReferenceId = bill.BillingReferenceId;
        _advancePayment = bill.AdvancePayment;
    }

    #endregion

    public int Id { get; set; }

    public string BillingReference {
        get
        {
            return _billingReferenceId;
        }
    }

    public string PaymentReference { 
        get
        {
            return _paymentReferenceId;
        }
    }

    public string MacAddress { 
        get
        {
            return _macAddress;
        }
    }

    public decimal PaidAmount { get; set; }

    public decimal BillingAmount 
    { 
        get
        {
            return _billingAmount;
        }
    }

    public PaymentMethod Method { get; set; }

    public int CreatedBy { get; set; }

    public DateTime DateCreated { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime DateUpdated { get; set; }

    public bool IsKeepChangeToAdvancePayment { get; set; }

    public bool Success { get; set; }

    public decimal TotalPayments 
    { 
        get
        {
            return _totalPayments;
        }
    }

    public abstract Task<bool> ProcessPayment(User user);

    protected bool CheckData()
    {
        bool isValid = false;
        StringBuilder sb = new StringBuilder();
        try
        { 
            if (this._billing.UnpaidAmount <= 0) sb.AppendLine("No unpaid amount to pay");
            if (this._billing == null || this._billing.Id == 0) sb.AppendLine("Billing information is not provided");
            if (this.PaidAmount == 0) sb.AppendLine("Amount to pay is not provided");

            if (!String.IsNullOrEmpty(sb.ToString().Trim())) throw new Exception(sb.ToString());
            isValid = true;
        }
        catch
        {
            throw;
        }

        return isValid;
    }

    private string GetPhysicalAddress()
    {
        string macAddress = string.Empty;

        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus == OperationalStatus.Up)
                macAddress += ni.GetPhysicalAddress().ToString();
        }
        return macAddress;
    }

}
