interface ErrorListProps {
  messages: string[]
}

export function ErrorList({ messages }: ErrorListProps) {
  if (messages.length === 0) {
    return null
  }

  return (
    <div className="notice error" role="alert">
      {messages.map((message, index) => (
        <p key={index}>{message}</p>
      ))}
    </div>
  )
}
