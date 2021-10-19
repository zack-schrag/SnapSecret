using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapSecret.Domain
{
    public class SnapSecretError
    {
        private List<Exception> _exceptions;
        private string? _userMessage;
        private SnapSecretErrorType _errorType;

        public IReadOnlyCollection<Exception> Exceptions => _exceptions;
        public string? UserMessage => _userMessage;
        public SnapSecretErrorType ErrorType => _errorType;

        public SnapSecretError(SnapSecretErrorType errorType)
        {
            _exceptions = new List<Exception>();
            _errorType = errorType;
        }

        public SnapSecretError WithUserMessage(string message)
        {
            _userMessage = message;
            return this;
        }

        public SnapSecretError WithException(Exception e)
        {
            _exceptions.Add(e);
            return this;
        }

        public object ToResponse()
        {
            return new
            {
                message = _userMessage,
                errorType = _errorType
            };
        }
    }
}
